using PostAgent.Shared.Posts;

namespace PostAgent.Digest;

public sealed record DigestItem(PostEntity Post, bool IsInteresting, string? Insight = null);

public sealed record DigestReport(string? Overview, IReadOnlyList<DigestItem> Items)
{
    public static DigestReport Plain(IReadOnlyList<DigestItem> items) => new(null, items);

    public static DigestReport From(IReadOnlyList<DigestItem> items, DigestSummary summary)
    {
        var insights = summary.Items
            .GroupBy(i => i.Id)
            .ToDictionary(g => g.Key, g => NullIfBlank(g.First().Insight));

        return new DigestReport(
            NullIfBlank(summary.Overview),
            items.Select(item => item with { Insight = insights.GetValueOrDefault(item.Post.RowKey) }).ToList());
    }

    private static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
