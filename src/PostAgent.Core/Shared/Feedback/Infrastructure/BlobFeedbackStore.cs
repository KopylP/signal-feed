using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Shared.Feedback.Infrastructure;

public sealed class BlobFeedbackStore(BlobServiceClient blobs, ILogger<BlobFeedbackStore> log) : IFeedbackStore
{
    private const int Attempts = 3;

    private BlobClient Blob => blobs
        .GetBlobContainerClient(StorageNames.SettingsContainer)
        .GetBlobClient(StorageNames.FeedbackBlob);

    public async Task<IReadOnlyList<FeedbackItem>> LoadAsync(CancellationToken ct) => (await ReadAsync(ct)).Items;

    // Another vote saved at the same moment is a lost race: read again and add ours on top.
    public async Task RecordAsync(FeedbackItem item, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            var (items, etag) = await ReadAsync(ct);
            var json = JsonSerializer.Serialize(FeedbackLog.Add(items, item), FeedbackJson.Options);
            if (await Blob.TryUploadAsync(BinaryData.FromString(json), etag, ct))
            {
                return;
            }
        }

        throw new InvalidOperationException("The vote was not saved: settings/feedback.json kept changing.");
    }

    private async Task<(IReadOnlyList<FeedbackItem> Items, ETag? ETag)> ReadAsync(CancellationToken ct)
    {
        if (await Blob.TryDownloadAsync(ct) is not { } content)
        {
            return ([], null);
        }

        var items = FeedbackJson.Parse(content.Text);

        // The ETag stays even when nothing could be read, so the next vote replaces a damaged file (If-Match) instead of failing on it.
        if (items.Count == 0 && content.Text.Trim() is not ("[]" or ""))
        {
            log.LogWarning("settings/feedback.json could not be read, the next vote replaces it");
        }

        return (items, content.ETag);
    }
}
