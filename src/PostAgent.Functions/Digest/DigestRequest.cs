using System.Text.Json;

namespace PostAgent.Digest;

// One /digest as it travels through the digest-requests queue: whose request it is and the period in UTC.
public sealed record DigestRequest(long ChatId, DateTimeOffset Since, DateTimeOffset Until)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public string ToMessage() => JsonSerializer.Serialize(this, Options);

    // Null for anything that is not a complete request: such a message can never succeed, so it must not be retried.
    public static DigestRequest? Parse(string message)
    {
        try
        {
            var request = JsonSerializer.Deserialize<DigestRequest>(message, Options);
            return request is { ChatId: not 0 } && request.Since < request.Until ? request : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
