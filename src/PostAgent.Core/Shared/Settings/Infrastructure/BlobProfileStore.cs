using Azure;
using Azure.Storage.Blobs;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Shared.Settings.Infrastructure;

public sealed class BlobProfileStore(BlobServiceClient blobs) : IProfileStore
{
    private BlobClient Blob(long chatId) => blobs
        .GetBlobContainerClient(StorageNames.SettingsContainer)
        .GetBlobClient(StorageNames.ProfileBlobName(chatId));

    public async Task<ProfileHandle?> LoadAsync(long chatId, CancellationToken ct) =>
        await Blob(chatId).TryDownloadAsync(ct) is { } content ? new ProfileHandle(content.Text, content.ETag.ToString()) : null;

    public Task<bool> TrySaveAsync(long chatId, ProfileHandle? current, string text, CancellationToken ct) =>
        Blob(chatId).TryUploadAsync(BinaryData.FromString(text), current is null ? null : new ETag(current.ETag), ct);
}
