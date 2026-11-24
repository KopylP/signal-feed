namespace PostAgent.Digest;

public interface IDigestSummarizer
{
    Task<DigestReport> SummarizeAsync(IReadOnlyList<DigestItem> items, CancellationToken ct);
}
