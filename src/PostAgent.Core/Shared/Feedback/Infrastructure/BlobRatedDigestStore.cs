using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using PostAgent.Shared.Chat;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Shared.Feedback.Infrastructure;

public sealed class BlobRatedDigestStore(BlobServiceClient blobs) : IRatedDigestStore
{
    private const int Attempts = 3;

    private BlobClient BlobOf(long chatId) => blobs
        .GetBlobContainerClient(StorageNames.SettingsContainer)
        .GetBlobClient(StorageNames.RatedDigestsBlobName(chatId));

    public async Task<ReportSnapshot?> FindAsync(long chatId, string reportId, CancellationToken ct) =>
        (await ReadAsync(BlobOf(chatId), ct)).Reports.FirstOrDefault(r => r.ReportId == reportId);

    public async Task SaveAsync(long chatId, ReportSnapshot report, CancellationToken ct)
    {
        var blob = BlobOf(chatId);
        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            var (reports, etag) = await ReadAsync(blob, ct);
            var conditions = etag is null
                ? new BlobRequestConditions { IfNoneMatch = ETag.All }
                : new BlobRequestConditions { IfMatch = etag };
            try
            {
                await blob.UploadAsync(
                    BinaryData.FromString(RatedDigests.Write(RatedDigests.Add(reports, report))),
                    new BlobUploadOptions { Conditions = conditions },
                    ct);
                return;
            }
            catch (RequestFailedException ex) when (ex.Status is 404 or 409 or 412 && attempt < Attempts)
            {
                // Another /digest of this chat was saved at the same moment: read again and add ours on top.
            }
        }
    }

    private static async Task<(IReadOnlyList<ReportSnapshot> Reports, ETag? ETag)> ReadAsync(BlobClient blob, CancellationToken ct)
    {
        try
        {
            var result = (await blob.DownloadContentAsync(ct)).Value;
            return (RatedDigests.Parse(result.Content.ToString()), result.Details.ETag);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return ([], null);
        }
    }
}
