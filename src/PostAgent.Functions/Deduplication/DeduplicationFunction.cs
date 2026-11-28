using Microsoft.Azure.Functions.Worker;
using PostAgent.Shared.Infrastructure;
using PostAgent.Shared.Posts.Storage;

namespace PostAgent.Deduplication;

public sealed class DeduplicationFunction(PostDeduplicator deduplicator)
{
    [Function(nameof(DeduplicationFunction))]
    public Task Run(
        [QueueTrigger(StorageNames.DeduplicationQueue, Connection = "AzureWebJobsStorage")] string message,
        CancellationToken ct) =>
        deduplicator.DeduplicateAsync(PostQueue.Parse(message), ct);
}
