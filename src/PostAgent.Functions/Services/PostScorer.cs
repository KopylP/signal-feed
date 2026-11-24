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

    private readonly Lazy<ChatClient> _client = new(() =>
        new ChatClient(options.Value.OpenAIModel, options.Value.OpenAIApiKey));

    private readonly Lazy<string> _systemPrompt = new(() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "prompts", "scoring.md")));

    /// <summary>Записує оцінки в пости. Повертає id, для яких модель оцінки не дала. Помилка API — виняток.</summary>
    public async Task<IReadOnlyList<string>> ScoreAsync(IReadOnlyList<PostEntity> posts, CancellationToken ct)
    {
        var userMessage = JsonSerializer.Serialize(
            posts.Select(p => new { id = p.RowKey, source = p.Source, author = p.Author, text = p.Text }),
            RequestJson);

        var completionOptions = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "scored_posts",
                BinaryData.FromString(SchemaJson),
                jsonSchemaIsStrict: true),
        };

        ChatCompletion completion = await _client.Value.CompleteChatAsync(
            [new SystemChatMessage(_systemPrompt.Value), new UserChatMessage(userMessage)],
            completionOptions,
            ct);

        if (completion.Content.Count == 0)
        {
            throw new InvalidOperationException($"OpenAI returned no content (finish reason: {completion.FinishReason}).");
        }

        return ScoreParser.Apply(posts, ScoreParser.Parse(completion.Content[0].Text));
    }
}
