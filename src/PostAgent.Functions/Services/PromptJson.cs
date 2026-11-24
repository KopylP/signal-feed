using System.Text.Encodings.Web;
using System.Text.Json;

namespace PostAgent.Services;

/// <summary>JSON для повідомлення користувача в запиті до моделі.</summary>
public static class PromptJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // кирилиця без \uXXXX, менше токенів
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}
