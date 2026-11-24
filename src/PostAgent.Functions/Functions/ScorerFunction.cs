using Microsoft.Azure.Functions.Worker;
using PostAgent.Models;
using PostAgent.Services;

namespace PostAgent.Functions;

/// <summary>
/// Оцінює пачку постів з черги. Помилка API — виняток, щоб спрацювали повтори черги
/// (після 5 спроб повідомлення піде в to-score-poison).
/// </summary>
public sealed class ScorerFunction(PostRepository posts, PostScorer scorer, ILogger<ScorerFunction> log)
{
    [Function(nameof(ScorerFunction))]
    public async Task Run(
        [QueueTrigger(ScoringQueue.Name, Connection = "AzureWebJobsStorage")] string message,
        CancellationToken ct)
    {
        var pending = (await posts.GetAsync(ScoringQueue.Parse(message), ct))
            .Where(p => p.Status == PostStatus.New)
            .ToList();

        if (pending.Count == 0)
        {
            return;
        }

        foreach (var id in await scorer.ScoreAsync(pending, ct))
        {
            log.LogWarning("Model returned no score for {PostId}", id);
        }

        foreach (var post in pending.Where(p => p.Status == PostStatus.Scored))
        {
            await posts.UpdateAsync(post, ct);
        }
    }
}
