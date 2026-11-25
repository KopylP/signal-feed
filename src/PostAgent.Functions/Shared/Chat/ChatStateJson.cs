using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace PostAgent.Shared.Chat;

// The blob format. Reading is forgiving in two steps: an unreadable snapshot means "no state", but an unreadable
// session or id list only drops that part, so a changed agent library never loses the digest for the day.
public static class ChatStateJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Write(ChatState state) =>
        JsonSerializer.Serialize(new Dto(state.Report, state.ProcessedUpdateIds, state.Session), Options);

    public static bool TryRead(string json, [NotNullWhen(true)] out ChatState? state)
    {
        state = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("report", out var reportElement)
                || reportElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            // A snapshot with missing fields (an older shape, a hand edit) would fail later on every question, so it counts as unreadable here.
            var report = reportElement.Deserialize<ReportSnapshot>(Options);
            if (report is not { ReportId: not null, Entries: not null, NeedsVerification: not null })
            {
                return false;
            }

            state = new ChatState(report, ReadIds(doc.RootElement), ReadSession(doc.RootElement));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static List<long> ReadIds(JsonElement root)
    {
        try
        {
            return root.TryGetProperty("processedUpdateIds", out var ids) && ids.ValueKind == JsonValueKind.Array
                ? ids.Deserialize<List<long>>(Options) ?? []
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static JsonElement? ReadSession(JsonElement root) =>
        root.TryGetProperty("session", out var session) && session.ValueKind == JsonValueKind.Object
            ? session.Clone()
            : null;

    private sealed record Dto(ReportSnapshot Report, IReadOnlyList<long> ProcessedUpdateIds, JsonElement? Session);
}
