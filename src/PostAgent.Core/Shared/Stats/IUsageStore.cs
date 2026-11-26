namespace PostAgent.Shared.Stats;

public interface IUsageStore
{
    Task RecordAsync(DateOnly day, string purpose, long inputTokens, long outputTokens, CancellationToken ct);

    // The days from..to inclusive, one row per day and purpose.
    Task<IReadOnlyList<UsageDay>> LoadAsync(DateOnly from, DateOnly to, CancellationToken ct);
}
