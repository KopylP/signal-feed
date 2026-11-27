using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using PostAgent.Chat.Memory;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Chat.Infrastructure;

// One Markdown blob per note under reader-memory/<chat id>/ in the settings container, so the notes can be read and fixed by hand in the portal.
public sealed class BlobReaderMemoryStore(BlobServiceClient blobs) : IReaderMemoryStore
{
    private BlobContainerClient Container => blobs.GetBlobContainerClient(StorageNames.SettingsContainer);

    public async Task<IReadOnlyList<StoredMemoryNote>> ListAsync(long chatId, CancellationToken ct)
    {
        var prefix = StorageNames.ReaderMemoryPrefix(chatId);
        var notes = new List<StoredMemoryNote>();
        await foreach (var item in Container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, ct))
        {
            // A blob deleted between the listing and the read is simply not there any more.
            if (await Container.GetBlobClient(item.Name).TryDownloadAsync(ct) is { } content)
            {
                notes.Add(new StoredMemoryNote(new MemoryNote(item.Name[prefix.Length..], content.Text), content.ETag.ToString()));
            }
        }

        return notes.OrderBy(n => n.Note.FileName, StringComparer.Ordinal).ToList();
    }

    public Task<bool> TryCreateAsync(long chatId, MemoryNote note, CancellationToken ct) =>
        Blob(chatId, note.FileName).TryUploadAsync(BinaryData.FromString(note.Text), null, ct);

    public Task<bool> TryReplaceAsync(long chatId, StoredMemoryNote current, string text, CancellationToken ct) =>
        Blob(chatId, current.Note.FileName).TryUploadAsync(BinaryData.FromString(text), new ETag(current.ETag), ct);

    public Task<bool> TryDeleteAsync(long chatId, StoredMemoryNote current, CancellationToken ct) =>
        Blob(chatId, current.Note.FileName).TryDeleteAsync(new ETag(current.ETag), ct);

    public async Task<LegacyProfile?> LoadLegacyProfileAsync(long chatId, CancellationToken ct) =>
        await LegacyBlob(chatId).TryDownloadAsync(ct) is { } content ? new LegacyProfile(content.Text, content.LastModified, content.ETag.ToString()) : null;

    public Task<bool> TryDeleteLegacyProfileAsync(long chatId, LegacyProfile profile, CancellationToken ct) =>
        LegacyBlob(chatId).TryDeleteAsync(new ETag(profile.ETag), ct);

    private BlobClient Blob(long chatId, string fileName) => Container.GetBlobClient(StorageNames.ReaderMemoryPrefix(chatId) + fileName);

    private BlobClient LegacyBlob(long chatId) => Container.GetBlobClient(StorageNames.LegacyProfileBlobName(chatId));
}
