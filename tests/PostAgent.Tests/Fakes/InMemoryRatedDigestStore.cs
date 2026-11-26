using PostAgent.Shared.Chat;
using PostAgent.Shared.Feedback;

namespace PostAgent.Tests.Fakes;

public sealed class InMemoryRatedDigestStore : IRatedDigestStore
{
    private readonly Dictionary<long, IReadOnlyList<ReportSnapshot>> _reports = [];

    public bool ThrowOnSave { get; set; }

    public bool ThrowOnFind { get; set; }

    public IReadOnlyList<ReportSnapshot> Of(long chatId) => _reports.GetValueOrDefault(chatId, []);

    public Task<ReportSnapshot?> FindAsync(long chatId, string reportId, CancellationToken ct) =>
        ThrowOnFind
            ? throw new InvalidOperationException("storage down")
            : Task.FromResult(Of(chatId).FirstOrDefault(r => r.ReportId == reportId));

    public Task SaveAsync(long chatId, ReportSnapshot report, CancellationToken ct)
    {
        if (ThrowOnSave)
        {
            throw new InvalidOperationException("storage down");
        }

        _reports[chatId] = RatedDigests.Add(Of(chatId), report);
        return Task.CompletedTask;
    }
}
