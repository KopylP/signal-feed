using System.Globalization;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Shared.Stats.Infrastructure;

public sealed class BlobUsageStore(BlobServiceClient blobs) : IUsageStore
{
    private const int Attempts = 3;

    private BlobClient Blob => blobs.GetBlobContainerClient(StorageNames.SettingsContainer).GetBlobClient(StorageNames.UsageBlob);

    public async Task RecordAsync(DateOnly day, string purpose, long inputTokens, long outputTokens, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            var (rows, etag) = await ReadAsync(ct);
            var conditions = etag is null
                ? new BlobRequestConditions { IfNoneMatch = ETag.All }
                : new BlobRequestConditions { IfMatch = etag };
            try
            {
                await Blob.UploadAsync(
                    BinaryData.FromString(JsonSerializer.Serialize(UsageLog.Add(rows, day, purpose, inputTokens, outputTokens), StatsJson.Options)),
                    new BlobUploadOptions { Conditions = conditions },
                    ct);
                return;
            }
            catch (RequestFailedException ex) when (ex.Status is 404 or 409 or 412 && attempt < Attempts)
            {
                // Another call was recorded at the same moment: read again and add ours on top.
            }
        }
    }

    public async Task<IReadOnlyList<UsageDay>> LoadAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var (rows, _) = await ReadAsync(ct);
        string Name(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return rows.Where(r => string.CompareOrdinal(r.Day, Name(from)) >= 0 && string.CompareOrdinal(r.Day, Name(to)) <= 0).ToList();
    }

    // The ETag stays for a damaged file, so the next call replaces it instead of failing on it.
    private async Task<(IReadOnlyList<UsageDay> Rows, ETag? ETag)> ReadAsync(CancellationToken ct)
    {
        try
        {
            var result = (await Blob.DownloadContentAsync(ct)).Value;
            var rows = StatsJson.TryParse<List<UsageDay>>(result.Content.ToString())?.Where(r => r is { Day: not null, Purpose: not null }).ToList() ?? [];
            return (rows, result.Details.ETag);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return ([], null);
        }
    }
}
