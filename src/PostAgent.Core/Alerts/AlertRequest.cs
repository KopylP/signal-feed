using System.Text.Json;
using System.Text.Json.Serialization;

namespace PostAgent.Alerts;

// A decided alert on its way to AlertSenderFunction (queue to-alert): the sender formats and sends it, it does not choose again.
public sealed record AlertRequest(AlertKind Kind, List<AlertedPost> Posts)
{
    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    public string ToMessage() => JsonSerializer.Serialize(this, Json);

    public static AlertRequest Parse(string message) =>
        JsonSerializer.Deserialize<AlertRequest>(message, Json) ?? throw new FormatException("An empty alert request");
}
