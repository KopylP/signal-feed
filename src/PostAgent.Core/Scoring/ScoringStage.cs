using PostAgent.Shared.Alerts;
using PostAgent.Shared.Posts;

namespace PostAgent.Scoring;

// One message of to-score: scores the new posts, saves them, and decides which of them to alert about (ADR-0024).
public sealed class ScoringStage(IPostRepository posts, IPostScorer scorer, IAlertDecider alerts, ILogger<ScoringStage> log)
{
    public async Task ScoreAsync(IReadOnlyList<PostRef> refs, CancellationToken ct)
    {
        var unscored = (await posts.GetAsync(refs, ct)).Where(p => p.Status == PostStatus.New).ToList();
        if (unscored.Count == 0)
        {
            return;
        }

        var missing = await scorer.ScoreAsync(unscored, ct);
        var scored = unscored.Where(p => p.Status == PostStatus.Scored).ToList();
        await posts.SaveScoresAsync(scored, ct);
        await TryDecideAlertsAsync(scored, ct);

        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"Model returned no score for: {string.Join(", ", missing)}");
        }
    }

    // A retry of this message would find the posts scored and decide nothing, so a failed decision is logged, not rethrown:
    // its posts go to the digest.
    private async Task TryDecideAlertsAsync(IReadOnlyList<PostEntity> scored, CancellationToken ct)
    {
        try
        {
            await alerts.DecideAsync(scored, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "The alert decision for {Count} scored posts failed, they go to the digest", scored.Count);
        }
    }
}
