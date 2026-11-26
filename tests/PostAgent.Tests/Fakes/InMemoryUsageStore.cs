using PostAgent.Shared.Stats;

namespace PostAgent.Tests.Fakes;

public sealed class InMemoryUsageStore : IUsageStore
{
    private IReadOnlyList<UsageDay> _rows = [];

    public bool Throw { get; set; }

    public IReadOnlyList<UsageDay> Rows => _rows;

    public void Seed(params UsageDay[] rows) => _rows = rows;

    public Task RecordAsync(DateOnly day, string purpose, long inputTokens, long outputTokens, CancellationToken ct)
    {
        if (Throw)
        {
            throw new InvalidOperationException("storage down");
        }

        _rows = UsageLog.Add(_rows, day, purpose, inputTokens, outputTokens);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<UsageDay>> LoadAsync(DateOnly from, DateOnly to, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<UsageDay>>(_rows
            .Where(r => string.CompareOrdinal(r.Day, from.ToString("yyyy-MM-dd")) >= 0 && string.CompareOrdinal(r.Day, to.ToString("yyyy-MM-dd")) <= 0).ToList());
}
