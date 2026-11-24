using PostAgent.Models;

namespace PostAgent.Services;

/// <summary>Оцінює пачку постів за промптом prompts/scoring.md.</summary>
public sealed class PostScorer(StructuredChat chat)
{
    private const string SchemaJson = """
        {"type":"object","properties":{"posts":{"type":"array","items":{"type":"object",
        "properties":{"id":{"type":"string"},"score":{"type":"integer"},
        "topic":{"type":"string"},"reason":{"type":"string"},
        "duplicate_of":{"type":["string","null"]}},
        "required":["id","score","topic","reason","duplicate_of"],
        "additionalProperties":false}}},"required":["posts"],"additionalProperties":false}
        """;

    private readonly string _systemPrompt = Prompts.Load("scoring.md");

    /// <summary>Записує оцінки в пости. Повертає id, для яких модель оцінки не дала. Помилка API — виняток.</summary>
    public async Task<IReadOnlyList<string>> ScoreAsync(IReadOnlyList<PostEntity> posts, CancellationToken ct)
    {
        var json = await chat.CompleteJsonAsync(_systemPrompt, ToUserMessage(posts), "scored_posts", SchemaJson, ct);
        return ScoreParser.Apply(posts, ScoreParser.Parse(json));
    }

    private static string ToUserMessage(IEnumerable<PostEntity> posts) =>
        StructuredChat.ToJson(posts.Select(p => new { id = p.RowKey, source = p.Source, author = p.Author, text = p.Text }));
}
