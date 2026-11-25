using Microsoft.Extensions.AI;
using PostAgent.Shared.Ai;
using PostAgent.Shared.Common;

namespace PostAgent.Digest.Infrastructure;

public sealed class DigestSummarizer(IChatClient chat) : IDigestSummarizer
{
    private const int MaxTextChars = 1500;

    private readonly string _systemPrompt = Prompts.Load("digest.md");

    public async Task<DigestReport> SummarizeAsync(IReadOnlyList<DigestItem> items, CancellationToken ct)
    {
        var summary = await chat.AskAsync<DigestSummary>(_systemPrompt, ToUserMessage(items), ct);
        var report = DigestReport.From(items, summary);

        // Strict mode checks the shape, not the ids. If every named id is unknown, the digest would be empty
        // and its posts still marked as sent; failing here makes the job send the plain digest instead.
        // A summary that names no post at all is a weak day, not an error.
        if (!report.HasEntries && NamesAnyPost(summary))
        {
            throw new InvalidOperationException("The digest summary named none of the selected posts.");
        }

        return report;
    }

    private static bool NamesAnyPost(DigestSummary summary) =>
        (summary.Headlines?.Count ?? 0) + (summary.ForMarketplace?.Count ?? 0)
        + (summary.Notable?.Count ?? 0) + (summary.Recommended?.Count ?? 0) > 0;

    private static string ToUserMessage(IEnumerable<DigestItem> items) =>
        PromptJson.Serialize(items.Select(i => new
        {
            id = i.Post.RowKey,
            source = i.Post.Source,
            published = i.Post.PublishedAt.UtcDateTime.ToString("yyyy-MM-dd"),
            topic = i.Post.Topic,
            score = i.Post.Score,
            interesting = i.IsInteresting,
            text = i.Post.Text.Truncate(MaxTextChars),
        }));
}
