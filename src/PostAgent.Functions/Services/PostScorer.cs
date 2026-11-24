using Microsoft.Extensions.AI;
using PostAgent.Models;

namespace PostAgent.Services;

/// <summary>Оцінює пачку постів за промптом prompts/scoring.md.</summary>
public sealed class PostScorer(IChatClient chat)
{
    private readonly string _systemPrompt = Prompts.Load("scoring.md");

    /// <summary>Записує оцінки в пости. Повертає id, для яких модель оцінки не дала. Помилка API — виняток.</summary>
    public async Task<IReadOnlyList<string>> ScoreAsync(IReadOnlyList<PostEntity> posts, CancellationToken ct)
    {
        var scored = await chat.AskAsync<ScoredPosts>(_systemPrompt, ToUserMessage(posts), ct);
        return ScoreApplier.Apply(posts, scored.Posts);
    }

    private static string ToUserMessage(IEnumerable<PostEntity> posts) =>
        PromptJson.Serialize(posts.Select(p => new { id = p.RowKey, source = p.Source, author = p.Author, text = p.Text }));
}
