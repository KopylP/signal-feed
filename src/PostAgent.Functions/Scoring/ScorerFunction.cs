using Microsoft.Azure.Functions.Worker;
using PostAgent.Posts;

namespace PostAgent.Scoring;

public sealed class ScorerFunction(IPostRepository posts, IPostScorer scorer)
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
