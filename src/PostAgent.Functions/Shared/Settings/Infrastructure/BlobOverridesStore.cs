using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Shared.Settings.Infrastructure;

public sealed class BlobOverridesStore(BlobServiceClient blobs, ILogger<BlobOverridesStore> log) : IOverridesStore
{
    private readonly BlobClient _blob = blobs
        .GetBlobContainerClient(StorageNames.SettingsContainer)
        .GetBlobClient(StorageNames.SettingsBlob);

    public async Task<OverridesHandle?> LoadAsync(CancellationToken ct)
    {
        try
        {
            var result = (await _blob.DownloadContentAsync(ct)).Value;
            if (SettingsOverridesJson.TryRead(result.Content.ToString(), out var overrides))
            {
                return new OverridesHandle(overrides, result.Details.ETag.ToString());
            }

            // Empty overrides with the real ETag: the next save replaces the damaged blob instead of failing forever.
            log.LogWarning("Settings overrides could not be read and are ignored");
            return new OverridesHandle(SettingsOverrides.Empty, result.Details.ETag.ToString());
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<bool> TrySaveAsync(OverridesHandle? current, SettingsOverrides updated, CancellationToken ct)
    {
        var conditions = current is null
            ? new BlobRequestConditions { IfNoneMatch = ETag.All }
            : new BlobRequestConditions { IfMatch = new ETag(current.ETag) };
        try
        {
            await _blob.UploadAsync(
                BinaryData.FromString(SettingsOverridesJson.Write(updated)),
                new BlobUploadOptions { Conditions = conditions },
                ct);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status is 404 or 409 or 412)
        {
            return false;
        }
    }
}
