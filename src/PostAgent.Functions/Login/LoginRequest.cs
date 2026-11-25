using System.Text.Json;

namespace PostAgent.Login;

// The queue message that starts the waiting side of a sign-in.
public sealed record LoginRequest(string AttemptId, long ChatId)
{
    public string ToMessage() => JsonSerializer.Serialize(this);

    public static LoginRequest? Parse(string message)
    {
        try
        {
            var request = JsonSerializer.Deserialize<LoginRequest>(message);
            return request is { AttemptId.Length: > 0, ChatId: not 0 } ? request : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
