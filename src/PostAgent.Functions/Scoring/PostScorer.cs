using Microsoft.Extensions.AI;
using PostAgent.Ai;
using PostAgent.Posts;

namespace PostAgent.Scoring;

public sealed class PostScorer(IChatClient chat) : IPostScorer
{
    private readonly string _systemPrompt = Prompts.Load("scoring.md");

    public async Task<IReadOnlyList<string>> ScoreAsync(IReadOnlyList<PostEntity> posts, CancellationToken ct)
    {
        var scored = await chat.AskAsync<ScoredPosts>(_systemPrompt, ToUserMessage(posts), ct);
        return ScoreApplier.Apply(posts, scored.Posts);
    }

    private static string ToUserMessage(IEnumerable<PostEntity> posts) =>
        PromptJson.Serialize(posts.Select(p => new { id = p.RowKey, source = p.Source, author = p.Author, text = p.Text }));
}
