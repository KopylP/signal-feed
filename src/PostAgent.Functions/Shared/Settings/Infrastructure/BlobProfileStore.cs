using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Shared.Settings.Infrastructure;

public sealed class BlobProfileStore(BlobServiceClient blobs) : IProfileStore
{
    private BlobClient Blob(long chatId) => blobs
        .GetBlobContainerClient(StorageNames.SettingsContainer)
        .GetBlobClient(StorageNames.ProfileBlobName(chatId));

    public async Task<ProfileHandle?> LoadAsync(long chatId, CancellationToken ct)
    {
        try
        {
            var result = (await Blob(chatId).DownloadContentAsync(ct)).Value;
            return new ProfileHandle(result.Content.ToString(), result.Details.ETag.ToString());
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<bool> TrySaveAsync(long chatId, ProfileHandle? current, string text, CancellationToken ct)
    {
        var conditions = current is null
            ? new BlobRequestConditions { IfNoneMatch = ETag.All }
            : new BlobRequestConditions { IfMatch = new ETag(current.ETag) };
        try
        {
            await Blob(chatId).UploadAsync(BinaryData.FromString(text), new BlobUploadOptions { Conditions = conditions }, ct);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status is 404 or 409 or 412)
        {
            return false;
        }
    }
}
