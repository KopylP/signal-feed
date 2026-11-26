namespace PostAgent.Shared.Stats;

public interface IDailyStatsStore
{
    // Replaces the day: a later run sees more scored posts and writes a fuller summary.
    Task SaveAsync(DailyStats stats, CancellationToken ct);

    // The stored days from..to inclusive, oldest first; a day that was never stored is simply missing.
    Task<IReadOnlyList<DailyStats>> LoadAsync(DateOnly from, DateOnly to, CancellationToken ct);

    // Deletes the days before the cutoff and returns how many.
    Task<int> DeleteOlderThanAsync(DateOnly cutoff, CancellationToken ct);
}
