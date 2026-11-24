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
        var unscored = (await posts.GetAsync(ScoringQueue.Parse(message), ct))
            .Where(p => p.Status == PostStatus.New)
            .ToList();
        if (unscored.Count == 0)
        {
            return;
        }

        foreach (var id in await scorer.ScoreAsync(unscored, ct))
        {
            log.LogWarning("Model returned no score for {PostId}", id);
        }

        await posts.UpdateAllAsync(unscored.Where(p => p.Status == PostStatus.Scored), ct);
    }
}
