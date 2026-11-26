using System.ComponentModel;
using Microsoft.Extensions.AI;
using PostAgent.Shared.Chat;
using PostAgent.Shared.Common;
using PostAgent.Shared.Feedback;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Agent.Tools;

// Not invasive (ADR-0021): a vote is only a hint for the scoring and the next vote on the post replaces it, so it is saved at once.
public sealed class RatePostTool(IPostRepository posts, IFeedbackStore feedback, IEffectiveOptions options, TimeProvider time, ILogger<RatePostTool> log) : IChatTool
{
    private const string NotFound = "Post not found. Ask the reader which post they mean, or find it with search_posts.";

    public AIFunction Build(ChatTurn turn) => AIFunctionFactory.Create(
        (
            [Description("The post id from the digest or from a search result, like 2026-11-25/channel_123.")] string id,
            [Description("true when the reader liked the post (👍, +, лайк, цікаво), false when not (👎, -, мінус, не цікаво).")] bool liked,
            CancellationToken ct) => RunAsync(id, liked, ct),
        new AIFunctionFactoryOptions
        {
            Name = "rate_post",
            Description = "Save the reader's like or dislike of one post now, no confirmation needed. The recent votes steer the scoring of new posts toward the reader's taste. Use it when the reader reacts to a news item (often a reply or a quote of it) with a like, a plus, a dislike, or says it is (not) interesting. If it is not clear which post they mean, ask.",
        });

    private async Task<string> RunAsync(string id, bool liked, CancellationToken ct)
    {
        if (!(await options.GetAsync(ct)).FeedbackEnabled)
        {
            return "Not saved: rating is switched off (the FeedbackEnabled setting). Tell the reader.";
        }

        if (!ChatPostId.TryParse(id, out var reference) || (await posts.GetAsync([reference], ct)).FirstOrDefault() is not { } post)
        {
            return NotFound;
        }

        try
        {
            var vote = liked ? FeedbackVote.Up : FeedbackVote.Down;
            await feedback.RecordAsync(new FeedbackItem(ChatPostId.Format(reference), post.Source, Topic(post), vote, time.GetUtcNow()), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "A vote could not be saved");
            return "Not saved: the storage failed. Tell the reader to try again.";
        }

        return liked
            ? "Done: the like is saved; posts like this will score a little higher. Tell the reader briefly."
            : "Done: the dislike is saved; posts like this will score a little lower. Tell the reader briefly.";
    }

    // What the scorer reads as the topic: the scoring topic, or the start of the post, cut so that a vote stays small.
    private static string Topic(PostEntity post) =>
        (string.IsNullOrWhiteSpace(post.Topic) ? post.Text : post.Topic).Truncate(FeedbackLog.MaxTopicChars, "…") is { Length: > 0 } topic ? topic : post.Source;
}
