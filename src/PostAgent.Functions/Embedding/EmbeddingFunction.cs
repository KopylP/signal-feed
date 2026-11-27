using Microsoft.Azure.Functions.Worker;
using PostAgent.Shared.Infrastructure;
using PostAgent.Shared.Posts.Storage;

namespace PostAgent.Embedding;

public sealed class EmbeddingFunction(PostEmbedder embedder)
{
    [Function(nameof(EmbeddingFunction))]
    public Task Run(
        [QueueTrigger(StorageNames.CollectedQueue, Connection = "AzureWebJobsStorage")] string message,
        CancellationToken ct) =>
        embedder.EmbedAsync(PostQueue.Parse(message), ct);
}
