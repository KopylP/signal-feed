using Microsoft.Extensions.AI;
using PostAgent.Shared.Ai;
using PostAgent.Shared.Feedback;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;

namespace PostAgent.Scoring.Infrastructure;

public sealed class PostScorer(IChatClient chat, IPromptProvider prompts, IFeedbackStore feedback, IEffectiveOptions options, ILogger<PostScorer> log) : IPostScorer
{
    public async Task<IReadOnlyList<string>> ScoreAsync(IReadOnlyList<PostEntity> posts, CancellationToken ct)
    {
        var userMessage = await FeedbackBlockAsync(ct) is { Length: > 0 } block
            ? block + "\n\n" + ToUserMessage(posts)
            : ToUserMessage(posts);
        var scored = await chat.AskAsync<ScoredPosts>(await prompts.GetAsync("scoring.md", ct), userMessage, ct);
        return ScoreApplier.Apply(posts, scored.Posts);
    }

    // A hint about taste, never a reason to fail or delay scoring: without the votes the posts are scored by the rules alone.
    private async Task<string> FeedbackBlockAsync(CancellationToken ct)
    {
        try
        {
            return (await options.GetAsync(ct)).FeedbackEnabled
                ? FeedbackPrompt.Block(await feedback.LoadAsync(ct))
                : string.Empty;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "The reader's feedback could not be read, scoring without it");
            return string.Empty;
        }
    }

    private static string ToUserMessage(IEnumerable<PostEntity> posts) =>
        PromptJson.Serialize(posts.Select(p => new { id = p.RowKey, source = p.Source, author = p.Author, text = p.Text }));
}
