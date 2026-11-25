using Microsoft.Extensions.AI;
using PostAgent.Shared.Ai;
using PostAgent.Shared.Posts;

namespace PostAgent.Scoring.Infrastructure;

public sealed class PostScorer(IChatClient chat, IPromptProvider prompts) : IPostScorer
{
    public async Task<IReadOnlyList<string>> ScoreAsync(IReadOnlyList<PostEntity> posts, CancellationToken ct)
    {
        var scored = await chat.AskAsync<ScoredPosts>(await prompts.GetAsync("scoring.md", ct), ToUserMessage(posts), ct);
        return ScoreApplier.Apply(posts, scored.Posts);
    }

    private static string ToUserMessage(IEnumerable<PostEntity> posts) =>
        PromptJson.Serialize(posts.Select(p => new { id = p.RowKey, source = p.Source, author = p.Author, text = p.Text }));
}
