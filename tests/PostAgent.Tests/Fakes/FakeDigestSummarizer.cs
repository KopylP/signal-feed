using PostAgent.Digest;

namespace PostAgent.Tests.Fakes;

internal sealed class FakeDigestSummarizer(Func<IReadOnlyList<DigestItem>, DigestReport> summarize) : IDigestSummarizer
{
    public Task<DigestReport> SummarizeAsync(IReadOnlyList<DigestItem> items, CancellationToken ct) =>
        Task.FromResult(summarize(items));
}
