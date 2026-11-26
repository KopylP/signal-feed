using PostAgent.Shared.Cleanup;
using PostAgent.Shared.Settings;
using PostAgent.Shared.Stats;

namespace PostAgent.Cleanup;

// Deletes the daily summaries (the long memory, ADR-0018) older than StatsRetentionDays.
public sealed class OldDailySummariesCleanup(IDailyStatsStore store, IEffectiveOptions options) : ICleanupTask
{
    public string Name => "OldDailySummaries";

    public async Task<int> RunAsync(CleanupRequest request, DateTimeOffset now, CancellationToken ct) =>
        await store.DeleteOlderThanAsync(DateOnly.FromDateTime(now.UtcDateTime).AddDays(-(await options.GetAsync(ct)).StatsRetentionDays), ct);
}
