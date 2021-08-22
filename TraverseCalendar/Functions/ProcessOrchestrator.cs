using Ical.Net;
using Ical.Net.CalendarComponents;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TraverseCalendar.Entities;
using TraverseCalendar.Models;

namespace TraverseCalendar.Functions
{
    public class ProcessOrchestrator
    {
        private readonly HttpClient httpClient;
        private const string entityInstanceKey = "calendarEntries";

        public ProcessOrchestrator(IHttpClientFactory httpClientFactory) => httpClient = httpClientFactory.CreateClient();

        [FunctionName(nameof(ProcessOrchestrator))]
        public async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            OrchestratorInput oi = context.GetInput<OrchestratorInput>();
            List<Event> currentEvents = await context.CallActivityAsync<List<Event>>(nameof(GetCalendar), oi.Url);
            List<Event> knownEvents = await GetCurrentKnownEventsAsync(context);
            var newEvents = currentEvents.Except(knownEvents).ToList();

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
            string url = Environment.GetEnvironmentVariable(iCalFeedEnvVar) ?? throw new ArgumentNullException(iCalFeedEnvVar);
            OrchestratorInput oi = new OrchestratorInput() { Url = url };

            string instanceId = await starter.StartNewAsync(nameof(ProcessOrchestrator), "FindNewCalendarEvents", oi);
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        private class OrchestratorInput
        {
            public string Url { get; set; } = string.Empty;
        }

        private class AllEvents
        {
            public List<Event> KnownEvents { get; set; } = new List<Event>();
            public List<Event> CurrentEvents { get; set; } = new List<Event>();
        }
    }
}