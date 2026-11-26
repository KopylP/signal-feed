using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;
using PostAgent.Shared.Stats;

namespace PostAgent.Cleanup;

// Long memory (ADR-0018): before PostsCleanup deletes a day, its counts are written as one small summary (no post text), and summaries
// older than StatsRetentionDays go. It runs first, and every run rewrites the complete days still in Posts, so a late scored post is not lost.
public sealed class DailyStatsTask(IPostRepository posts, IDailyStatsStore store, IEffectiveOptions options, ILogger<DailyStatsTask> log) : ICleanupTask
{
    public string Name => "DailyStats";

    public async Task<int> RunAsync(DateTimeOffset now, CancellationToken ct)
    {
        var settings = await options.GetAsync(ct);
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var since = new DateTimeOffset(today.AddDays(-settings.RetentionDays).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var written = 0;
        foreach (var group in (await posts.GetSinceAsync(since, now, ct)).GroupBy(p => p.PartitionKey).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            // Today is not finished: its posts are still arriving and being scored.
            if (group.Key == today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))
            {
                continue;
            }

            if (DailyStatsBuilder.Build(group.Key, group, settings.InterestingThreshold) is { } stats)
            {
                await store.SaveAsync(stats, ct);
                written++;
            }
        }

        var deleted = await store.DeleteOlderThanAsync(today.AddDays(-settings.StatsRetentionDays), ct);
        log.LogInformation("Daily summaries: {Written} written, {Deleted} deleted", written, deleted);
        return deleted;
    }
}
