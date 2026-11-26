using Microsoft.Azure.Functions.Worker;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Digest;

public sealed class DigestRequestFunction(OnDemandDigest digest, ILogger<DigestRequestFunction> log)
{
    [Function(nameof(DigestRequestFunction))]
    public async Task Run(
        [QueueTrigger(StorageNames.DigestRequestQueue, Connection = "AzureWebJobsStorage")] string message,
        CancellationToken ct)
    {
        var request = DigestRequest.Parse(message);
        if (request is null)
        {
            // Retrying a message that cannot be read only ends in the poison queue.
            log.LogWarning("A malformed digest request was dropped");
            return;
        }

        await digest.RunAsync(request, ct);
    }
}
