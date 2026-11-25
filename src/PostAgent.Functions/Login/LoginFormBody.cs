using System.Text.Json.Serialization;

namespace PostAgent.Login;

// The JSON the Mini App posts. Every field is checked by LoginForm before it is used.
public sealed record LoginFormBody(
    [property: JsonPropertyName("attempt")] string? Attempt,
    [property: JsonPropertyName("initData")] string? InitData,
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("password")] string? Password);
