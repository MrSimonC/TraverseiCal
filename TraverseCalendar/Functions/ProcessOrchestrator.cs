using Ical.Net;
using Ical.Net.CalendarComponents;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using TraverseCalendar.Entities;
using TraverseCalendar.Models;

namespace TraverseCalendar.Functions
{
    public class ProcessOrchestrator
    {
        private readonly HttpClient httpClient;
        public ProcessOrchestrator(IHttpClientFactory httpClientFactory) => httpClient = httpClientFactory.CreateClient();

        [FunctionName(nameof(ProcessOrchestrator))]
        public async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            string iCalFeedEnvVar = "HTTPS_ICAL_FEED";
            string url = Environment.GetEnvironmentVariable(iCalFeedEnvVar) ?? throw new ArgumentNullException(iCalFeedEnvVar);
            var calendar = await context.CallActivityAsync<ICalendarObject>(nameof(GetCalendarAsync), url);
            var newEvents = await context.CallActivityAsync<List<Event>>(nameof(FindNewEvents), calendar);
            
        }

        [FunctionName("GetCalendar")]
        public async Task<Calendar> GetCalendarAsync([ActivityTrigger] string url, ILogger log)
        {
            var content = await httpClient.GetByteArrayAsync(url);
            using var stream = new MemoryStream(content);
            var calendar = Calendar.Load(stream);
            return calendar;
        }

        [FunctionName("FindNewEvents")]
        public List<Event> FindNewEvents([ActivityTrigger] Calendar liveCal, ILogger log, IDurableOrchestrationContext context)
        {
            // get current stored events
            var eventEntity = new EntityId(nameof(EventsEntity), "calendarEntries");
            var eventEntityProxy = context.CreateEntityProxy<EventsEntity>(eventEntity);
            var store = eventEntityProxy.GetEvents();

            List<Event> result = new();

            foreach (var calEvent in liveCal.Events)
            {
                Event model = new()
                {
                    DateUTC = calEvent.DtStart.AsUtc,
                    Subject = calEvent.Summary,
                    Uid = calEvent.Uid
                };

                if (!store.Contains(model))
                {
                    result.Add(model);
                }
            }

            return result;
        }

        [FunctionName("Http_ProcessStart")]
        public async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync(nameof(ProcessOrchestrator), "FindNewCalendarEvents");

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}