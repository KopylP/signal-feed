using Microsoft.Extensions.AI;
using PostAgent.Ai;
using PostAgent.Common;

namespace PostAgent.Digest;

public sealed class DigestSummarizer(IChatClient chat) : IDigestSummarizer
{
    private const int MaxTextChars = 1000;

    private readonly string _systemPrompt = Prompts.Load("digest.md");

    public async Task<DigestReport> SummarizeAsync(IReadOnlyList<DigestItem> items, CancellationToken ct)
    {
        var summary = await chat.AskAsync<DigestSummary>(_systemPrompt, ToUserMessage(items), ct);
        return DigestReport.From(items, summary);
    }

    private static string ToUserMessage(IEnumerable<DigestItem> items) =>
        PromptJson.Serialize(items.Select(i => new
        {
            id = i.Post.RowKey,
            source = i.Post.Source,
            topic = i.Post.Topic,
            score = i.Post.Score,
            text = i.Post.Text.Truncate(MaxTextChars),
        }));
}
