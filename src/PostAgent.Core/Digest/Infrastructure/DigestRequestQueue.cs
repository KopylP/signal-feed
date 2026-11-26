using Azure.Storage.Queues;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Digest.Infrastructure;

public sealed class DigestRequestQueue([FromKeyedServices(StorageNames.DigestRequestQueue)] QueueClient queue) : IDigestRequestQueue
{
    // A digest that waited longer than this is not wanted any more: the reader would get it long after asking.
    private static readonly TimeSpan TimeToLive = TimeSpan.FromMinutes(30);

    public async Task EnqueueAsync(DigestRequest request, CancellationToken ct)
    {
        // The runtime creates the queue when the function starts; this covers a request that comes first.
        await queue.CreateIfNotExistsAsync(cancellationToken: ct);
        await queue.SendMessageAsync(request.ToMessage(), timeToLive: TimeToLive, cancellationToken: ct);
    }
}
