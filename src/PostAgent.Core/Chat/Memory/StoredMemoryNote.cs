namespace PostAgent.Chat.Memory;

// A note as it was read, with the version a conditional replace or delete must match.
public sealed record StoredMemoryNote(MemoryNote Note, string ETag);
