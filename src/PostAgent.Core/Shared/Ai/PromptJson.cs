using System.Text.Encodings.Web;
using System.Text.Json;

namespace PostAgent.Shared.Ai;

public static class PromptJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // кирилиця без \uXXXX, менше токенів
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}
