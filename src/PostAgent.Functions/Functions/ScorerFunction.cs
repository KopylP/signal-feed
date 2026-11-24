using Microsoft.Azure.Functions.Worker;
using PostAgent.Models;
using PostAgent.Services;

namespace PostAgent.Functions;

/// <summary>
/// Оцінює пачку постів з черги. Помилка API або пост без оцінки дають виняток, щоб спрацювали повтори черги
/// (після 5 спроб повідомлення піде в to-score-poison). Повтор бере лише пости зі статусом New.
/// </summary>
public sealed class ScorerFunction(PostRepository posts, PostScorer scorer)
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

        var missing = await scorer.ScoreAsync(unscored, ct);
        await posts.SaveScoresAsync(unscored.Where(p => p.Status == PostStatus.Scored), ct);

        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"Model returned no score for: {string.Join(", ", missing)}");
        }
    }
}
