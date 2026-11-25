using System.Text.Json.Serialization;

namespace PostAgent.Login;

// The JSON the Mini App posts. Every field is checked by LoginForm before it is used. There is no password field:
// the page sends only the SRP proof (srpA, srpM1) it computed from the password.
public sealed record LoginFormBody(
    [property: JsonPropertyName("attempt")] string? Attempt,
    [property: JsonPropertyName("initData")] string? InitData,
    [property: JsonPropertyName("action")] string? Action,
    [property: JsonPropertyName("phone")] string? Phone,
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("srpA")] string? SrpA,
    [property: JsonPropertyName("srpM1")] string? SrpM1);
