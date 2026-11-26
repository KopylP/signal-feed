using PostAgent.Shared.Cleanup;

namespace PostAgent.Cleanup;

// One cleanup of data that is no longer needed. CleanupFunction runs every task registered in DI after DailySummaryJob has written
// the daily summaries, so a new cleanup (another table, queue or blob) is a new class and one line in CleanupServices.cs.
public interface ICleanupTask
{
    string Name { get; }

    // Deletes what is stale and returns how many records were deleted. Never touch a day after request.SummarizedThrough.
    Task<int> RunAsync(CleanupRequest request, DateTimeOffset now, CancellationToken ct);
}
