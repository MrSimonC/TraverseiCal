using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace TraverseCalendar.Functions;

public class RaiseEvents
{

    [FunctionName(nameof(RaiseApprovalEvent))]
    public static async Task RaiseApprovalEvent(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
        [DurableClient] IDurableOrchestrationClient client,
        ILogger log)
    {
        log.LogInformation($"Running {nameof(RaiseApprovalEvent)}");

        if (!bool.TryParse(req.Query["approvestate"], out bool approveState))
        {
            throw new FunctionFailedException($"{nameof(approveState)} is needed", new ArgumentOutOfRangeException(nameof(approveState)));
        }

        string? instanceId = req.Query["instanceid"];
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            throw new FunctionFailedException($"{nameof(instanceId)} is needed", new ArgumentOutOfRangeException(nameof(instanceId)));
        }

        await client.RaiseEventAsync(instanceId, EventNames.ApprovalEventName, approveState);
    }
}
