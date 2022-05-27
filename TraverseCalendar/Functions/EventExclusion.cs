using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using TraverseCalendar.Models;
using static TraverseCalendar.Constants;

namespace TraverseCalendar.Functions;

public class EventExclusion
{
    [FunctionName("AddEventExclusion")]
    [return: Table(tableName)]
    public static ExcludeEventTableEntity TableOutput(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] string eventName,
        ILogger log)
    {
        log.LogInformation($"Adding excluded event: {eventName}");
        return new ExcludeEventTableEntity
        {
            PartitionKey = partitionKey,
            RowKey = Guid.NewGuid().ToString(),
            Subject = eventName
        };
    }

    [FunctionName("GetEventExclusions")]
    public static async Task<ActionResult> TableInputAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
        [Table(tableName, partitionKey)] TableClient tableClient,
        ILogger log)
    {
        log.LogInformation($"Getting events from table: {tableName} with partionKey {partitionKey}");
        AsyncPageable<ExcludeEventTableEntity> queryResults = tableClient.QueryAsync<ExcludeEventTableEntity>(filter: $"PartitionKey eq '{partitionKey}'");
        var results = await queryResults.Select(e => e.Subject).ToListAsync();
        return new OkObjectResult(results);
    }
}
