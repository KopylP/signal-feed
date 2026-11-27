namespace PostAgent.Chat.Memory;

// The notes of one chat's reader memory, one file each. A chat never reads another chat's notes. Writers use the ETag.
public interface IReaderMemoryStore
{
    // Sorted by file name, so the oldest day comes first.
    Task<IReadOnlyList<StoredMemoryNote>> ListAsync(long chatId, CancellationToken ct);

    // False when a note with this file name already exists.
    Task<bool> TryCreateAsync(long chatId, MemoryNote note, CancellationToken ct);

    // Writes over the version that was read. False when it changed or was deleted meanwhile.
    Task<bool> TryReplaceAsync(long chatId, StoredMemoryNote current, string text, CancellationToken ct);

    // False when the note changed or was already gone.
    Task<bool> TryDeleteAsync(long chatId, StoredMemoryNote current, CancellationToken ct);

    // The profile blob written before ADR-0022; null when there is none (the usual case after the first question).
    Task<LegacyProfile?> LoadLegacyProfileAsync(long chatId, CancellationToken ct);

    Task<bool> TryDeleteLegacyProfileAsync(long chatId, LegacyProfile profile, CancellationToken ct);
}
