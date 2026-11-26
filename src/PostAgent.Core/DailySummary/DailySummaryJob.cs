using System.Globalization;
using PostAgent.Shared.Cleanup;
using PostAgent.Shared.Jobs;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;
using PostAgent.Shared.Stats;

namespace PostAgent.DailySummary;

// Long memory (ADR-0018): writes a small summary (counts, sources, topics, the best posts; no post text) of every finished day still in Posts,
// then tells the cleanup that the summaries are written. If anything here fails, no request is queued, so no post is deleted without its summary.
public sealed class DailySummaryJob(
    IPostRepository posts,
    IDailyStatsStore store,
    ICleanupRequestQueue cleanup,
    IEffectiveOptions options,
    TimeProvider time,
    ILogger<DailySummaryJob> log) : IJob
{
    public string Name => "daily-summary";

    public async Task RunAsync(CancellationToken ct)
    {
        try
        {
            await SummarizeAndRequestCleanupAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Nothing is queued, so old posts and conversations are NOT deleted until a run succeeds: the operator must see this.
            log.LogError(ex, "Daily summaries failed: the cleanup of old data is held back until this succeeds");
            throw;
        }
    }

    private async Task SummarizeAndRequestCleanupAsync(CancellationToken ct)
    {
        var settings = await options.GetAsync(ct);
        var now = time.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var todayName = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var written = 0;
        // Every post still stored, however old: a cleanup that was held back leaves older days behind, and they get their summary too.
        foreach (var day in (await posts.GetSinceAsync(DateTimeOffset.UnixEpoch, now, ct)).GroupBy(p => p.PartitionKey).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            // Today is not finished: its posts are still arriving and being scored.
            if (day.Key == todayName)
            {
                continue;
            }

            if (DailyStatsBuilder.Build(day.Key, day, settings.InterestingThreshold) is { } stats && !await HasMoreAsync(stats, ct))
            {
                await store.SaveAsync(stats, ct);
                written++;
            }
        }

        await cleanup.EnqueueAsync(CleanupRequest.For(today.AddDays(-1)), ct);
        log.LogInformation("Daily summaries: {Written} written, cleanup requested through {Day}", written, today.AddDays(-1));
    }

    // A partition left half-deleted by a failed cleanup would give a smaller summary than the one already stored: the fuller one stays.
    private async Task<bool> HasMoreAsync(DailyStats stats, CancellationToken ct)
    {
        var day = DateOnly.ParseExact(stats.Day, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return (await store.LoadAsync(day, day, ct)).Any(existing => existing.Collected > stats.Collected);
    }
}
