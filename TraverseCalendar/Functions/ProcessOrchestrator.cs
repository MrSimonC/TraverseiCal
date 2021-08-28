using Ical.Net;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Prowl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Todoist.Net;
using Todoist.Net.Models;
using TraverseCalendar.Entities;
using TraverseCalendar.Helpers;
using TraverseCalendar.Models;

namespace TraverseCalendar.Functions
{
    public partial class ProcessOrchestrator
    {
        private readonly HttpClient httpClient;
        private readonly ITodoistClient todoistClient;
        private const string entityInstanceKey = "calendarEntries";
        private readonly IProwlMessage prowlMessage;

        public ProcessOrchestrator(IHttpClientFactory httpClientFactory,
            IProwlMessage prowlMessage)
        {
            httpClient = httpClientFactory.CreateClient();
            todoistClient = new TodoistClient(Environment.GetEnvironmentVariable("TODOIST_API_KEY") ?? throw new NullReferenceException("Missing TODOIST_API_KEY environment variable"));
            this.prowlMessage = prowlMessage;
        }

        [FunctionName(nameof(ProcessOrchestrator))]
        public async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
        {
            log.LogInformation("Orchestrator running...");
            OrchestratorInput oi = context.GetInput<OrchestratorInput>();

            List<Event> currentEvents = await context.CallActivityAsync<List<Event>>(nameof(GetCalendarAsync), oi.ICalFeedUrl);
            IEventsEntity knownEventsEntityProxy = context.CreateEntityProxy<IEventsEntity>(entityInstanceKey);
            List<Event> knownEvents = await knownEventsEntityProxy.GetEventsAsync();
            var newEvents = currentEvents.Except(knownEvents).ToList();
            log.LogInformation($"Found {newEvents.Count} new events.");

            if (!newEvents.Any())
            {
                log.LogInformation($"Found no new events - so exiting.");
                return;
            }

            await ProcessAndSaveNewEvents(context, log, oi, knownEventsEntityProxy, newEvents);
        }

        private async Task ProcessAndSaveNewEvents(
            IDurableOrchestrationContext context,
            ILogger log,
            OrchestratorInput oi,
            IEventsEntity knownEventsEntityProxy,
            List<Event> newEvents)
        {
            // if many new entries, it's likely first run, so update Entity, but don't update Todoist
            if (newEvents.Count > 20)
            {
                log.LogInformation("Overwriting all existing events");
                knownEventsEntityProxy.SetEvents(newEvents);
            }
            else
            {
                foreach (Event newEvent in newEvents)
                {
                    await context.CallActivityAsync(nameof(SendProwlMessage), newEvent.Subject);
                    await context.CallActivityAsync(nameof(AddEventToTodoistList), (oi.TodoistList, newEvent.Subject));
                    knownEventsEntityProxy.AddEvent(newEvent);
                }
            }
        }

        [FunctionName(nameof(GetCalendarAsync))]
        public async Task<List<Event>> GetCalendarAsync(
            [ActivityTrigger] string url,
            ILogger log)
        {
            log.LogInformation($"{nameof(GetCalendarAsync)}: getting url {url}");
            byte[]? content = await httpClient.GetByteArrayAsync(url);
            using var stream = new MemoryStream(content);
            var calendar = Calendar.Load(stream);
            log.LogInformation($"{nameof(GetCalendarAsync)}: returning with {calendar.Events.Count} calendar events");
            return ICalHelper.ConvertICalToEvents(calendar);
        }

        [FunctionName(nameof(SendProwlMessage))]
        public async Task SendProwlMessage(
            [ActivityTrigger] string message,
            ILogger log)
        {
            log.LogInformation($"About to send message to prowl: {message}");
            HttpResponseMessage? result = await prowlMessage.SendAsync(message, application: "iCal Todoist", @event: "New Event Found");
            result.EnsureSuccessStatusCode();
        }

        [FunctionName(nameof(AddEventToTodoistList))]
        public async Task AddEventToTodoistList(
            [ActivityTrigger] (string projectName, string entry) projEntry,
            ILogger log)
        {
            log.LogInformation($"Getting todoist list id (for list: {projEntry.projectName})");
            IEnumerable<Project>? projects = await todoistClient.Projects.GetAsync();
            ComplexId projectId = projects
                .Where(p => string.Equals(projEntry.projectName, p.Name, StringComparison.InvariantCultureIgnoreCase))
                .Select(p => p.Id)
                .Single();
            log.LogInformation($"Found projectId: {projectId} for project name: {projEntry.projectName}.");

            log.LogInformation($"Adding event: {projEntry.entry} to Todoist project with id: {projectId}");
            await todoistClient.Items.AddAsync(new Item(projEntry.entry, projectId));
        }

        [FunctionName(nameof(Http_ProcessStart))]
        public async Task<HttpResponseMessage> Http_ProcessStart(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            OrchestratorInput oi = GetEnvironmentVars();

            string instanceId = await starter.StartNewAsync(nameof(ProcessOrchestrator), oi);
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName(nameof(Timer_ProcessStartAsync))]
        public static async Task Timer_ProcessStartAsync(
            [TimerTrigger("0 0 */4 * * *")] TimerInfo myTimer,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            if (myTimer.IsPastDue)
            {
                log.LogInformation("Was triggered after past due. Exit.");
                return;
            }
            log.LogInformation($"{nameof(Timer_ProcessStartAsync)} trigger function executed at: {DateTime.Now}");
            OrchestratorInput oi = GetEnvironmentVars();

            string instanceId = await starter.StartNewAsync(nameof(ProcessOrchestrator), oi);
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
        }

        private static OrchestratorInput GetEnvironmentVars()
        {
            return new OrchestratorInput()
            {
                ICalFeedUrl = Environment.GetEnvironmentVariable("HTTPS_ICAL_FEED") ?? throw new ArgumentNullException("HTTPS_ICAL_FEED"),
                TodoistList = Environment.GetEnvironmentVariable("TODOIST_LIST") ?? throw new ArgumentNullException("TODOIST_LIST")
            };
        }
    }
}