using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Shared.Settings.Infrastructure;

public sealed class BlobProfileStore(BlobServiceClient blobs) : IProfileStore
{
    private readonly BlobClient _blob = blobs
        .GetBlobContainerClient(StorageNames.SettingsContainer)
        .GetBlobClient(StorageNames.ProfileBlob);

    public async Task<ProfileHandle?> LoadAsync(CancellationToken ct)
    {
        try
        {
            var result = (await _blob.DownloadContentAsync(ct)).Value;
            return new ProfileHandle(result.Content.ToString(), result.Details.ETag.ToString());
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<bool> TrySaveAsync(ProfileHandle? current, string text, CancellationToken ct)
    {
        var conditions = current is null
            ? new BlobRequestConditions { IfNoneMatch = ETag.All }
            : new BlobRequestConditions { IfMatch = new ETag(current.ETag) };
        try
        {
            await _blob.UploadAsync(BinaryData.FromString(text), new BlobUploadOptions { Conditions = conditions }, ct);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status is 404 or 409 or 412)
        {
            return false;
        }
    }
}
