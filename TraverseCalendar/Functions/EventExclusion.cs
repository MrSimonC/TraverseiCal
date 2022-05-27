using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using TraverseCalendar.Models;

namespace TraverseiCal.Functions;

public class EventExclusion
{
    [FunctionName("AddEventExclusion")]
    [return: Table("ExcludedEvents", "events")]
    public static ExcludeEventTableEntity TableOutput(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] string eventName, 
        ILogger log)
    {
        log.LogInformation($"Adding excluded event: {eventName}");
        return new ExcludeEventTableEntity { 
            RowKey = Guid.NewGuid().ToString(), 
            Subject = eventName
        };
    }
}
