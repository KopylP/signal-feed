using System.Text.Json;

namespace PostAgent.Chat;

// One question from the reader, as it travels through the chat queue. ReportId says which digest it was asked about.
public sealed record ChatRequest(long UpdateId, string Text, string ReportId)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public string ToMessage() => JsonSerializer.Serialize(this, Options);

    // Null for anything that is not a complete request: such a message can never succeed, so it must not be retried.
    public static ChatRequest? Parse(string message)
    {
        try
        {
            var request = JsonSerializer.Deserialize<ChatRequest>(message, Options);
            return request is { UpdateId: not 0, Text: not null, ReportId: not null } ? request : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
