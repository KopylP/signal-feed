using System.Globalization;
using PostAgent.Shared.Stats;

namespace PostAgent.Tests.Fakes;

public sealed class InMemoryDailyStatsStore : IDailyStatsStore
{
    private readonly Dictionary<string, DailyStats> _days = [];

    public IReadOnlyDictionary<string, DailyStats> Days => _days;

    public void Seed(params DailyStats[] stats)
    {
        foreach (var s in stats)
        {
            _days[s.Day] = s;
        }
    }

    public Task SaveAsync(DailyStats stats, CancellationToken ct)
    {
        _days[stats.Day] = stats;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DailyStats>> LoadAsync(DateOnly from, DateOnly to, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DailyStats>>(_days.Values
            .Where(d => string.CompareOrdinal(d.Day, Name(from)) >= 0 && string.CompareOrdinal(d.Day, Name(to)) <= 0)
            .OrderBy(d => d.Day, StringComparer.Ordinal).ToList());

    public Task<int> DeleteOlderThanAsync(DateOnly cutoff, CancellationToken ct)
    {
        var old = _days.Keys.Where(k => string.CompareOrdinal(k, Name(cutoff)) < 0).ToList();
        old.ForEach(k => _days.Remove(k));
        return Task.FromResult(old.Count);
    }

    private static string Name(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
