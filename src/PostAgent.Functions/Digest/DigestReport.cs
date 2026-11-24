using PostAgent.Digest.Infrastructure;
using PostAgent.Shared.Posts;

namespace PostAgent.Digest;

// What goes into the digest message: the model's texts tied to our posts by id.
public sealed record DigestReport(
    string? Overview,
    IReadOnlyList<DigestEntry> Headlines,
    IReadOnlyList<DigestEntry> ForMarketplace,
    IReadOnlyList<DigestEntry> Notable,
    IReadOnlyList<DigestEntry> Recommended,
    IReadOnlyList<string> NeedsVerification,
    DigestSkipped Skipped)
{
    public const int MaxRecommended = 3;
    public const int MaxNeedsVerification = 5;

    // Without the model: the selected posts with the short reason from scoring.
    public static DigestReport Plain(IReadOnlyList<DigestItem> items) => new(
        Overview: null,
        Headlines: items.Select(i => new DigestEntry(i.Post, i.Post.Topic, i.Post.Reason ?? string.Empty)).ToList(),
        ForMarketplace: [],
        Notable: [],
        Recommended: [],
        NeedsVerification: [],
        Skipped: DigestSkipped.None);

    public static DigestReport From(IReadOnlyList<DigestItem> items, DigestSummary summary)
    {
        var posts = items.GroupBy(i => i.Post.RowKey).ToDictionary(g => g.Key, g => g.First().Post);
        var shown = new HashSet<string>();

        // A post is shown once across the three main sections; the first section that names it wins.
        var headlines = Entries(summary.Headlines, h => (h.Id, h.Title, h.Summary, h.WhyItMatters), posts, shown);
        var forMarketplace = Entries(summary.ForMarketplace, n => (n.Id, n.Title, n.Note, null), posts, shown);
        var notable = Entries(summary.Notable, n => (n.Id, n.Title, n.Note, null), posts, shown);

        // Recommended posts may repeat a main one: it is a different question ("worth reading in full").
        var recommended = Entries(summary.Recommended, p => (p.Id, null, p.Why, null), posts, new HashSet<string>())
            .Take(MaxRecommended)
            .ToList();

        var referenced = shown.Concat(recommended.Select(e => e.Post.RowKey)).ToHashSet();
        var notShown = posts.Keys.Count(key => !referenced.Contains(key));

        return new DigestReport(
            NullIfBlank(summary.Overview),
            headlines,
            forMarketplace,
            notable,
            recommended,
            (summary.NeedsVerification ?? [])
                .Select(NullIfBlank)
                .OfType<string>()
                .Distinct()
                .Take(MaxNeedsVerification)
                .ToList(),
            new DigestSkipped(Duplicates: 0, Other: notShown));
    }

    public DigestReport WithSkipped(DigestSkipped more) => this with { Skipped = Skipped.Add(more) };

    private static List<DigestEntry> Entries<T>(
        IEnumerable<T>? source,
        Func<T, (string Id, string? Title, string? Text, string? Why)> read,
        IReadOnlyDictionary<string, PostEntity> posts,
        HashSet<string> used)
    {
        var entries = new List<DigestEntry>();
        foreach (var item in source ?? [])
        {
            var (id, title, text, why) = read(item);
            var body = NullIfBlank(text);
            if (id is null || body is null || !posts.TryGetValue(id, out var post) || !used.Add(id))
            {
                continue;
            }

            entries.Add(new DigestEntry(post, NullIfBlank(title), body, NullIfBlank(why)));
        }

        return entries;
    }

    private static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
