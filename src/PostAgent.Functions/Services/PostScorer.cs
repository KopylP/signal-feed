using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using PostAgent.Models;

namespace PostAgent.Services;

/// <summary>Оцінює пачку постів через OpenAI (Structured Outputs, strict).</summary>
public sealed class PostScorer(IOptions<AgentOptions> options)
{
    private const string SchemaJson = """
        {"type":"object","properties":{"posts":{"type":"array","items":{"type":"object",
        "properties":{"id":{"type":"string"},"score":{"type":"integer"},
        "topic":{"type":"string"},"reason":{"type":"string"},
        "duplicate_of":{"type":["string","null"]}},
        "required":["id","score","topic","reason","duplicate_of"],
        "additionalProperties":false}}},"required":["posts"],"additionalProperties":false}
        """;

    private static readonly JsonSerializerOptions RequestJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // кирилиця без \uXXXX, менше токенів
    };

    private readonly ChatClient _client = new(options.Value.OpenAIModel, options.Value.OpenAIApiKey);
    private readonly string _systemPrompt = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "prompts", "scoring.md"));

    /// <summary>Записує оцінки в пости. Повертає id, для яких модель оцінки не дала. Помилка API — виняток.</summary>
    public async Task<IReadOnlyList<string>> ScoreAsync(IReadOnlyList<PostEntity> posts, CancellationToken ct)
    {
        var completion = await _client.CompleteChatAsync(
            [new SystemChatMessage(_systemPrompt), new UserChatMessage(ToUserMessage(posts))],
            new ChatCompletionOptions { ResponseFormat = StrictResponseFormat() },
            ct);

        if (completion.Value.Content.Count == 0)
        {
            throw new InvalidOperationException($"OpenAI returned no content (finish reason: {completion.Value.FinishReason}).");
        }

        return ScoreParser.Apply(posts, ScoreParser.Parse(completion.Value.Content[0].Text));
    }

    private static ChatResponseFormat StrictResponseFormat() =>
        ChatResponseFormat.CreateJsonSchemaFormat("scored_posts", BinaryData.FromString(SchemaJson), jsonSchemaIsStrict: true);

    private static string ToUserMessage(IEnumerable<PostEntity> posts) =>
        JsonSerializer.Serialize(
            posts.Select(p => new { id = p.RowKey, source = p.Source, author = p.Author, text = p.Text }),
            RequestJson);
}
