namespace PostAgent.Services;

/// <summary>Просить модель підсумувати відібрані пости: що головне сьогодні й що цікавого в кожному (prompts/digest.md).</summary>
public sealed class DigestSummarizer(StructuredChat chat)
{
    private const string SchemaJson = """
        {"type":"object","properties":{"overview":{"type":"string"},
        "items":{"type":"array","items":{"type":"object",
        "properties":{"id":{"type":"string"},"insight":{"type":"string"}},
        "required":["id","insight"],"additionalProperties":false}}},
        "required":["overview","items"],"additionalProperties":false}
        """;

    private const int MaxTextChars = 1000;

    private readonly string _systemPrompt = Prompts.Load("digest.md");

    public async Task<DigestReport> SummarizeAsync(IReadOnlyList<DigestItem> items, CancellationToken ct)
    {
        var json = await chat.CompleteJsonAsync(_systemPrompt, ToUserMessage(items), "digest_summary", SchemaJson, ct);
        return DigestReport.Parse(items, json);
    }

    private static string ToUserMessage(IEnumerable<DigestItem> items) =>
        StructuredChat.ToJson(items.Select(i => new
        {
            id = i.Post.RowKey,
            source = i.Post.Source,
            topic = i.Post.Topic,
            score = i.Post.Score,
            text = i.Post.Text.Truncate(MaxTextChars),
        }));
}
