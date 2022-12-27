using Azure;
using Azure.Data.Tables;
using Ical.Net;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Prowl;
using Prowl.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using TraverseCalendar.Entities;
using TraverseCalendar.Helpers;
using TraverseCalendar.Models;
using TraverseCalendar.Policies;
using static TraverseCalendar.Constants;

namespace TraverseCalendar.Functions;

public partial class ProcessOrchestrator
{
    private readonly HttpClient httpClient;
    private readonly HttpClient httpTodoistClient;
    const string todoistApiBaseUrl = "https://api.todoist.com/rest/v2/";
    private readonly JsonSerializerOptions todoistJsonOpts = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = new SnakeCasePropertyNamingPolicy()};
    private const string entityInstanceKey = "calendarEntries";
    private readonly IProwlMessage prowlMessage;
    
    /// <summary>
    /// Url of Azure Client Function *with no trailing slash*, which will raise the event to the Orchestration
    /// </summary>
    private readonly string RaiseApprovalEventUrl = Environment.GetEnvironmentVariable("RAISE_APPROVAL_EVENT_URL") ?? throw new ArgumentNullException(nameof(RaiseApprovalEventUrl));
    private readonly string RegExToReplace = Environment.GetEnvironmentVariable("REGEX_TO_REPLACE") ?? throw new ArgumentNullException(nameof(RegExToReplace));

    public ProcessOrchestrator(IHttpClientFactory httpClientFactory,
        IProwlMessage prowlMessage)
    {
        httpClient = httpClientFactory.CreateClient();
        var todoistAPIKey = Environment.GetEnvironmentVariable("TODOIST_APIKEY") ?? throw new NullReferenceException("Missing TODOIST_APIKEY environment variable");
        this.httpTodoistClient = new HttpClient() { BaseAddress = new Uri(todoistApiBaseUrl) };
        this.httpTodoistClient.DefaultRequestHeaders.Authorization =  new AuthenticationHeaderValue("Bearer", todoistAPIKey);
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
        List<string> excludedEvents = await context.CallActivityAsync<List<string>>(nameof(GetExcludedEvents), null);
        var newEvents = currentEvents.Except(knownEvents).ToList();
        newEvents = newEvents.Where(e => !excludedEvents.Contains(e.Subject.Trim(), StringComparer.CurrentCultureIgnoreCase)).ToList();
        log.LogInformation($"Found events current/known/new: {currentEvents.Count}/{knownEvents.Count}/{newEvents.Count}");

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
        // if many new entries, it's likely first run, so overwrite Entity, but don't update Todoist
        if (newEvents.Count > 100)
        {
            log.LogInformation("Overwriting all existing events");
            knownEventsEntityProxy.SetEvents(newEvents);
        }
        else
        {
            foreach (Event newEvent in newEvents)
            {
                if (newEvent.DateUTC <= context.CurrentUtcDateTime)
                {
                    log.LogInformation($"Event is in the past for subject: {newEvent.Subject} with date: {newEvent.DateUTC}. Skip.");
                    knownEventsEntityProxy.AddEvent(newEvent);
                    continue;
                }

                var useAppleShortcuts = bool.Parse(Environment.GetEnvironmentVariable("USE_APPLE_SHORTCUTS") ?? "false");
                if (useAppleShortcuts)
                {
                    string url = $"shortcuts://run-shortcut?name=Raise Event&input=text&text={context.InstanceId}$$${newEvent.Subject}";
                    var prowlMsg = new ProwlMessageContents() { Description = newEvent.Subject, Application = "iCal Todoist", Event = "New Event", Url = url };
                    await context.CallActivityAsync(nameof(SendProwlMessage), prowlMsg);
                }
                else
                {
                    string url = $"{RaiseApprovalEventUrl}&approvestate=true&instanceid={context.InstanceId}";
                    var prowlMsg = new ProwlMessageContents() { Description = newEvent.Subject, Application = "iCal Todoist", Event = "Approve", Url = url };
                    await context.CallActivityAsync(nameof(SendProwlMessage), prowlMsg);

                    url = $"{RaiseApprovalEventUrl}&approvestate=false&instanceid={context.InstanceId}";
                    prowlMsg = new ProwlMessageContents() { Description = newEvent.Subject, Application = "iCal Todoist", Event = "Ignore", Url = url };
                    await context.CallActivityAsync(nameof(SendProwlMessage), prowlMsg);
                }

                bool approved = await context.WaitForExternalEvent<bool>(EventNames.ApprovalEventName);
                if (approved)
                {
                    await context.CallActivityAsync(nameof(AddEventToTodoistList), (oi.TodoistList, newEvent));
                }
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
        string iCalFile = await httpClient.GetStringAsync(url);
        log.LogInformation($"{nameof(GetCalendarAsync)}: replacing using regex {RegExToReplace}");
        log.LogInformation($"Regex Env Var found {Regex.Matches(iCalFile, RegExToReplace).Count} matches to replace with empty string");
        string iCalFileClean = Regex.Replace(iCalFile, RegExToReplace, string.Empty);
        log.LogInformation($"Regex found {Regex.Matches(iCalFileClean, @"(?<!\r)\\+n").Count} matches to replace with \\r\\n");
        iCalFileClean = Regex.Replace(iCalFileClean, @"(?<!\r)\\+n" ,@"\r\n");
        var calendar = Calendar.Load(iCalFileClean);
        log.LogInformation($"{nameof(GetCalendarAsync)}: returning with {calendar.Events.Count} calendar events");
        return ICalHelper.ConvertICalToEvents(calendar);
    }

    [FunctionName(nameof(GetExcludedEvents))]
    public async Task<List<string>> GetExcludedEvents(
        [ActivityTrigger] IDurableActivityContext conext,
        [Table(tableName, partitionKey)] TableClient tableClient,
        ILogger log)
    {
        log.LogInformation($"{nameof(GetExcludedEvents)}: getting events");
        AsyncPageable<ExcludeEventTableEntity> queryResults = tableClient.QueryAsync<ExcludeEventTableEntity>(filter: $"PartitionKey eq '{partitionKey}'");
        List<string> results = await queryResults.Select(e => e.Subject).ToListAsync();
        log.LogInformation($"{nameof(GetExcludedEvents)}: returning {results.Count} events");
        return results;
    }

    [FunctionName(nameof(SendProwlMessage))]
    public async Task SendProwlMessage(
        [ActivityTrigger] ProwlMessageContents prowlMessageContents,
        ILogger log)
    {
        log.LogInformation($"About to send message to prowl: {prowlMessageContents.Description} ({prowlMessageContents.Event})");
        HttpResponseMessage? result = await prowlMessage.SendAsync(prowlMessageContents);
        result.EnsureSuccessStatusCode();
    }

    [FunctionName(nameof(AddEventToTodoistList))]
    public async Task AddEventToTodoistList(
        [ActivityTrigger] (string projectName, Event evnt) projEvnt,
        ILogger log)
    {
        log.LogInformation($"Getting todoist list id (for list: {projEvnt.projectName})");
        IEnumerable<Project>? projects = await httpTodoistClient.GetFromJsonAsync<List<Project>>("projects", todoistJsonOpts);
        projects ??= Array.Empty<Project>();
        string projectId = projects
            .Where(p => string.Equals(projEvnt.projectName, p.Name, StringComparison.InvariantCultureIgnoreCase))
            .Select(p => p.Id)
            .Single();
        log.LogInformation($"Found projectId: {projectId} for project name: {projEvnt.projectName}.");

        log.LogInformation($"Adding event: {projEvnt.evnt.Subject} to Todoist project with id: {projectId}");
        var item = new Item()
        {
            Content = projEvnt.evnt.Subject,
            ProjectId = projectId,
            Due = new() {
                Date = projEvnt.evnt.DateUTC
            }
        };
        await httpTodoistClient.PostAsJsonAsync("tasks", item, todoistJsonOpts);
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
        [TimerTrigger("0 0 8-22/4 * * *")] TimerInfo myTimer, // every 4 hours between 8am-10pm
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