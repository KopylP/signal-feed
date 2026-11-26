using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Shared.Feedback.Infrastructure;

public sealed class BlobFeedbackStore(BlobServiceClient blobs, ILogger<BlobFeedbackStore> log) : IFeedbackStore
{
    private const int Attempts = 3;

    private BlobClient Blob => blobs
        .GetBlobContainerClient(StorageNames.SettingsContainer)
        .GetBlobClient(StorageNames.FeedbackBlob);

    public async Task<IReadOnlyList<FeedbackItem>> LoadAsync(CancellationToken ct) => (await ReadAsync(ct)).Items;

    public async Task RecordAsync(FeedbackItem item, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            var (items, etag) = await ReadAsync(ct);
            var conditions = etag is null
                ? new BlobRequestConditions { IfNoneMatch = ETag.All }
                : new BlobRequestConditions { IfMatch = etag };
            try
            {
                await Blob.UploadAsync(
                    BinaryData.FromString(JsonSerializer.Serialize(FeedbackLog.Add(items, item), FeedbackJson.Options)),
                    new BlobUploadOptions { Conditions = conditions },
                    ct);
                return;
            }
            catch (RequestFailedException ex) when (ex.Status is 404 or 409 or 412 && attempt < Attempts)
            {
                // Another vote was saved at the same moment: read again and add ours on top.
            }
        }
    }

    private async Task<(IReadOnlyList<FeedbackItem> Items, ETag? ETag)> ReadAsync(CancellationToken ct)
    {
        try
        {
            var result = (await Blob.DownloadContentAsync(ct)).Value;
            var content = result.Content.ToString();
            var items = FeedbackJson.Parse(content);

            // The ETag stays even when nothing could be read, so the next vote replaces a damaged file (If-Match) instead of failing on it.
            if (items.Count == 0 && content.Trim() is not ("[]" or ""))
            {
                log.LogWarning("settings/feedback.json could not be read, the next vote replaces it");
            }

            return (items, result.Details.ETag);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return ([], null);
        }
    }
}
