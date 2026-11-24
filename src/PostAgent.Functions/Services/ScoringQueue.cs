using System.Text.Json;
using Azure.Storage.Queues;
using PostAgent.Models;

namespace PostAgent.Services;

/// <summary>Черга to-score. Повідомлення містить лише посилання на пости, не тексти (ліміт 64 КБ).</summary>
public sealed class ScoringQueue(QueueClient queue)
{
    public const string Name = "to-score";

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
