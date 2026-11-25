namespace PostAgent.Shared.Chat;

// One digest entry as the reader saw it, plus a short excerpt of the post for the chat agent.
// PostId is the chat-facing id (see ChatPostId).
public sealed record SnapshotEntry(
    string Section,
    string PostId,
    string Source,
    DateTimeOffset PublishedAt,
    string? Title,
    string Text,
    string? Why,
    string Excerpt,
    int? Score,
    string Url);
