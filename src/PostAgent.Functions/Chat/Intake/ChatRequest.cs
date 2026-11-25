using System.Text.Json;

namespace PostAgent.Chat.Intake;

// One question from the reader, as it travels through the chat queue. ReportId says which digest it was asked about, ChatId whose question it is.
public sealed record ChatRequest(long UpdateId, string Text, string ReportId, long ChatId)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public string ToMessage() => JsonSerializer.Serialize(this, Options);

    // Null for anything that is not a complete request: such a message can never succeed, so it must not be retried.
    public static ChatRequest? Parse(string message)
    {
        try
        {
            var request = JsonSerializer.Deserialize<ChatRequest>(message, Options);
            return request is { UpdateId: not 0, Text: not null, ReportId: not null, ChatId: not 0 } ? request : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
