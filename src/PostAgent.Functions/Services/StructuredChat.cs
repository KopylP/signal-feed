using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace PostAgent.Services;

/// <summary>
/// Єдине місце, де код викликає OpenAI: системний промпт плюс повідомлення користувача, відповідь за
/// строгою JSON-схемою (Structured Outputs). Клієнт створюється ліниво, щоб відсутній ключ ламав
/// лише ті виклики, яким потрібна модель, а не весь застосунок.
/// </summary>
public sealed class StructuredChat(IOptions<AgentOptions> options)
{
    private static readonly JsonSerializerOptions RequestJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // кирилиця без \uXXXX, менше токенів
    };

    private readonly Lazy<ChatClient> _client = new(() =>
        new ChatClient(options.Value.OpenAIModel, options.Value.OpenAIApiKey));

    public static string ToJson<T>(T value) => JsonSerializer.Serialize(value, RequestJson);

    /// <summary>Повертає JSON відповіді. Помилка API — виняток.</summary>
    public async Task<string> CompleteJsonAsync(
        string systemPrompt, string userMessage, string schemaName, string schemaJson, CancellationToken ct)
    {
        var completion = await _client.Value.CompleteChatAsync(
            [new SystemChatMessage(systemPrompt), new UserChatMessage(userMessage)],
            new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    schemaName, BinaryData.FromString(schemaJson), jsonSchemaIsStrict: true),
            },
            ct);

        if (completion.Value.Content.Count == 0)
        {
            throw new InvalidOperationException($"OpenAI returned no content (finish reason: {completion.Value.FinishReason}).");
        }

        return completion.Value.Content[0].Text;
    }
}
