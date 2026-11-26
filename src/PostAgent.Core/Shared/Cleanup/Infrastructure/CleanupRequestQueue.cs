using Azure.Storage.Queues;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Shared.Cleanup.Infrastructure;

public sealed class CleanupRequestQueue([FromKeyedServices(StorageNames.CleanupRequestQueue)] QueueClient queue) : ICleanupRequestQueue
{
    // A request that waited longer than this is stale: the next daily summary queues a fresh one.
    private static readonly TimeSpan TimeToLive = TimeSpan.FromDays(1);

    public async Task EnqueueAsync(CleanupRequest request, CancellationToken ct)
    {
        // The runtime creates the queue when the function starts; this covers a request that comes first.
        await queue.CreateIfNotExistsAsync(cancellationToken: ct);
        await queue.SendMessageAsync(request.ToMessage(), timeToLive: TimeToLive, cancellationToken: ct);
    }
}
