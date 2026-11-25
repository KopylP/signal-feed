using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using PostAgent.Shared.Common;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Infrastructure.Tools;

// The hits are capped (ChatSearchMaxHits) and every post is inside a <post> block.
public sealed class SearchPostsTool(IPostRepository posts, IOptions<AgentOptions> options, TimeProvider time) : IChatTool
{
    private const int ExcerptChars = 300;

    public AIFunction Build(ProposalPlan plan) => AIFunctionFactory.Create(
        (
            [Description("Words to look for, for example: google core update.")] string query,
            [Description("How many days back to search, at least 1.")] int days,
            CancellationToken ct) => RunAsync(query, days, ct),
        new AIFunctionFactoryOptions
        {
            Name = "search_posts",
            Description = "Search the stored posts of the last days for the wider picture. Every word of the query must appear in the post. Returns a few short hits with ids.",
        });

    private async Task<string> RunAsync(string query, int days, CancellationToken ct)
    {
        var terms = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0)
        {
            return "The query is empty.";
        }

        var settings = options.Value;
        var now = time.GetUtcNow();
        var recent = await posts.GetSinceAsync(now.AddDays(-Math.Clamp(days, 1, settings.RetentionDays)), now, ct);

        var hits = recent
            .Where(p => terms.All(t => p.Text.Contains(t, StringComparison.OrdinalIgnoreCase)
                || (p.Topic?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false)))
            .OrderByDescending(p => p.PublishedAt)
            .Take(settings.ChatSearchMaxHits)
            .ToList();

        return hits.Count == 0
            ? "No posts found."
            : string.Join("\n\n", hits.Select(p => DataBlock.Wrap("post", PostDescription.Of(p) + "\n" + p.Text.Truncate(ExcerptChars, "…"))));
    }
}
