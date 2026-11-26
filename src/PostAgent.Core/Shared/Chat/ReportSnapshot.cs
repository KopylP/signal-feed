using System.Text.Json.Serialization;

namespace PostAgent.Shared.Chat;

// What the last digest said, kept so the chat can answer about it (the digest itself is not stored anywhere else).
public sealed record ReportSnapshot(
    string ReportId,
    DateTimeOffset CreatedAt,
    string? Overview,
    IReadOnlyList<SnapshotEntry> Entries,
    IReadOnlyList<string> NeedsVerification)
{
    // A conversation can start before the first digest: it has a snapshot with no id and no entries, and the digest replaces it when it comes.
    public static ReportSnapshot None(DateTimeOffset now) => new(string.Empty, now, null, [], []);

    [JsonIgnore]
    public bool HasDigest => ReportId.Length > 0;
}
