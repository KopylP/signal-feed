namespace PostAgent.Shared.Chat;

// What the last digest said, kept so the chat can answer about it (the digest itself is not stored anywhere else).
public sealed record ReportSnapshot(
    string ReportId,
    DateTimeOffset CreatedAt,
    string? Overview,
    IReadOnlyList<SnapshotEntry> Entries,
    IReadOnlyList<string> NeedsVerification);
