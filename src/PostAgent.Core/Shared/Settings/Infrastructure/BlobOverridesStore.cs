using Azure;
using Azure.Storage.Blobs;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Shared.Settings.Infrastructure;

public sealed class BlobOverridesStore(BlobServiceClient blobs, ILogger<BlobOverridesStore> log) : IOverridesStore
{
    private readonly BlobClient _blob = blobs
        .GetBlobContainerClient(StorageNames.SettingsContainer)
        .GetBlobClient(StorageNames.SettingsBlob);

    public async Task<OverridesHandle?> LoadAsync(CancellationToken ct)
    {
        if (await _blob.TryDownloadAsync(ct) is not { } content)
        {
            return null;
        }

        if (SettingsOverridesJson.TryRead(content.Text, out var overrides))
        {
            return new OverridesHandle(overrides, content.ETag.ToString());
        }

        // Flagged and empty: ordinary saves refuse to overwrite it, an explicit reset replaces it (with the real ETag).
        log.LogWarning("Settings overrides could not be read and are ignored");
        return new OverridesHandle(SettingsOverrides.Empty, content.ETag.ToString(), Unreadable: true);
    }

    public Task<bool> TrySaveAsync(OverridesHandle? current, SettingsOverrides updated, CancellationToken ct) =>
        _blob.TryUploadAsync(BinaryData.FromString(SettingsOverridesJson.Write(updated)), current is null ? null : new ETag(current.ETag), ct);
}
