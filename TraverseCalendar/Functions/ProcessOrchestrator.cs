using Ical.Net;
using Ical.Net.CalendarComponents;
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

            // get current events, previously known events, and compare to find what's new
            List<Event> currentEvents = await context.CallActivityAsync<List<Event>>(nameof(GetCalendar), oi.iCalFeedUrl);
            IEventsEntity entityProxy = context.CreateEntityProxy<IEventsEntity>(entityInstanceKey);
            List<Event> knownEvents = await entityProxy.GetEventsAsync();
            var newEvents = currentEvents.Except(knownEvents).ToList();
            log.LogInformation($"Found {newEvents.Count} new events.");
            
            if (!newEvents.Any())
            {
                log.LogInformation($"Found no new events - so exiting.");
                return;
            }

            // if many new entries, it's likely first run, so update Entity, but don't update Todoist
            if (newEvents.Count > 20)
            {
                log.LogInformation("Overwriting all existing events");
                entityProxy.SetEvents(newEvents);
            }
            else
            {
                IEnumerable<Project>? projects = await todoistClient.Projects.GetAsync();
                ComplexId projectId = projects
                    .Where(p => string.Equals(oi.TodoistList.ToLowerInvariant(), p.Name.ToLowerInvariant()))
                    .Select(p => p.Id)
                    .Single();

                foreach (Event newEvent in newEvents)
                {
                    // first implementation: just send a notification.
                    await prowlMessage.SendAsync(newEvent.Subject);

                    // for later implementation:
                    //bool approved = await context.WaitForExternalEvent<bool>("Approval");
                    //if (approved)
                    //{
                    //    await todoistClient.Items.AddAsync(new Item(newEvent.Subject, projectId));
                    //}
                    entityProxy.AddEvent(newEvent);
                }
            }
        }

        [FunctionName(nameof(GetCalendar))]
        public async Task<List<Event>> GetCalendar(
            [ActivityTrigger] string url,
            ILogger log)
        {
            log.LogInformation($"{nameof(GetCalendar)}: getting url {url}");
            byte[]? content = await httpClient.GetByteArrayAsync(url);
            using var stream = new MemoryStream(content);
            var calendar = Calendar.Load(stream);
            log.LogInformation($"{nameof(GetCalendar)}: returning with {calendar.Events.Count} calendar events");
            return ConvertICalToEvents(calendar);
        }

        private static List<Event> ConvertICalToEvents(Calendar calendar)
        {
            var result = new List<Event>();

            foreach (CalendarEvent calEvent in calendar.Events)
            {
                result.Add(new Event()
                {
                    DateUTC = calEvent.DtStart.AsUtc,
                    Subject = calEvent.Summary,
                    Uid = calEvent.Uid
                });
            }
            return result;
        }

        private async Task<List<Event>> GetCurrentKnownEventsAsync(IDurableOrchestrationContext context)
        {
            var entity = new EntityId(nameof(EventsEntity), entityInstanceKey);
            IEventsEntity entityProxy = context.CreateEntityProxy<IEventsEntity>(entity);

            return await entityProxy.GetEventsAsync();
        }

        [FunctionName(nameof(Http_ProcessStart))]
        public async Task<HttpResponseMessage> Http_ProcessStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            string iCalFeedEnvVar = "HTTPS_ICAL_FEED";
            string iCalFeedUrl = Environment.GetEnvironmentVariable(iCalFeedEnvVar) ?? throw new ArgumentNullException(iCalFeedEnvVar);
            //string todoistApi = Environment.GetEnvironmentVariable("TODOIST_APIKEY") ?? throw new NullReferenceException("Missing TODOIST_APIKEY environment variable");
            string todoistList = Environment.GetEnvironmentVariable("TODOIST_LIST") ?? throw new NullReferenceException("Missing TODOIST_LIST environment variable");
            var oi = new OrchestratorInput()
            {
                iCalFeedUrl = iCalFeedUrl,
                //TodoistAPIKey = todoistApi,
                TodoistList = todoistList
            };

            string instanceId = await starter.StartNewAsync(nameof(ProcessOrchestrator), "FindNewCalendarEvents", oi);
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}