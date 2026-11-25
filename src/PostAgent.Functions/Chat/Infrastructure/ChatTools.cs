using System.ComponentModel;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using PostAgent.Shared.Chat;
using PostAgent.Shared.Common;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Infrastructure;

// The tools the chat agent may call. Post text is data written by others: it is always inside a <post> block.
public sealed class ChatTools(IPostRepository posts, IOptions<AgentOptions> options, TimeProvider time)
{
    private const int ExcerptChars = 300;
    private const string NotFound = "Post not found. It may have been deleted; rely on the digest text.";

    [Description("Get the full text and details of one stored post by its id (the id shown in the digest, like 2026-11-25/channel_123).")]
    public async Task<string> GetPost([Description("The post id from the digest or from a search result.")] string id, CancellationToken ct)
    {
        if (!ChatPostId.TryParse(id, out var reference))
        {
            return NotFound;
        }

        var post = (await posts.GetAsync([reference], ct)).FirstOrDefault();
        return post is null ? NotFound : Describe(post) + "\n" + DataBlock.Wrap("post", post.Text);
    }

    [Description("Search the stored posts of the last days for the wider picture. Every word of the query must appear in the post. Returns a few short hits with ids.")]
    public async Task<string> SearchPosts(
        [Description("Words to look for, for example: google core update.")] string query,
        [Description("How many days back to search, at least 1.")] int days,
        CancellationToken ct)
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
            : string.Join("\n\n", hits.Select(p => Describe(p) + "\n" + DataBlock.Wrap("post", p.Text.Truncate(ExcerptChars, "…"))));
    }

    private static string Describe(PostEntity post)
    {
        var line = new StringBuilder();
        line.Append("id: ").Append(ChatPostId.Format(post.ToRef()));
        line.Append(" | @").Append(DataBlock.Escape(post.Source));
        line.Append(" | ").Append(post.PublishedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).Append(" UTC");
        if (post.Topic is not null)
        {
            line.Append(" | ").Append(DataBlock.Escape(post.Topic));
        }

        if (post.Score is not null)
        {
            line.Append(" | score ").Append(post.Score.Value.ToString(CultureInfo.InvariantCulture));
        }

        return line.Append(" | ").Append(post.Url).ToString();
    }
}
