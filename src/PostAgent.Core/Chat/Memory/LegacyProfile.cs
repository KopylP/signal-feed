namespace PostAgent.Chat.Memory;

// The "about me" text of the reader profile that the reader memory replaced (ADR-0022), read once to be moved into a note.
public sealed record LegacyProfile(string Text, DateTimeOffset LastModified, string ETag);
