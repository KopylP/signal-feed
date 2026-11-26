using Microsoft.Azure.Functions.Worker;
using PostAgent.Shared.Cleanup;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Cleanup;

// Starts only from the request DailySummaryJob queues after the daily summaries are written (never on a timer of its own).
public sealed class CleanupFunction(CleanupRunner runner, ILogger<CleanupFunction> log)
{
    [Function(nameof(CleanupFunction))]
    public async Task Run(
        [QueueTrigger(StorageNames.CleanupRequestQueue, Connection = "AzureWebJobsStorage")] string message,
        CancellationToken ct)
    {
        var request = CleanupRequest.Parse(message);
        if (request is null)
        {
            // Retrying a message that cannot be read only ends in the poison queue.
            log.LogWarning("A malformed cleanup request was dropped");
            return;
        }

        await runner.RunAsync(request, ct);
    }
}
