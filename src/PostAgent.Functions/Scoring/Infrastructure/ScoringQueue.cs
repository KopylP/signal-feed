using System.Text.Json;
using Azure.Storage.Queues;
using PostAgent.Shared.Posts;

namespace PostAgent.Scoring.Infrastructure;

public sealed class ScoringQueue(QueueClient queue) : IScoringQueue
{
    public async Task EnqueueAsync(IReadOnlyList<PostRef> refs, int batchSize, CancellationToken ct)
    {
        foreach (var batch in refs.Chunk(batchSize))
        {
            await queue.SendMessageAsync(JsonSerializer.Serialize(batch), ct);
        }
    }

    public static IReadOnlyList<PostRef> Parse(string message) =>
        JsonSerializer.Deserialize<PostRef[]>(message) ?? [];
}
