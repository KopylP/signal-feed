using System.Globalization;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Shared.Stats.Infrastructure;

public sealed class BlobUsageStore(BlobServiceClient blobs) : IUsageStore
{
    private const int Attempts = 3;

    private BlobClient Blob => blobs.GetBlobContainerClient(StorageNames.SettingsContainer).GetBlobClient(StorageNames.UsageBlob);

    // Another call recorded at the same moment is a lost race: read again and add ours on top.
    public async Task RecordAsync(DateOnly day, string purpose, long inputTokens, long outputTokens, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            var (rows, etag) = await ReadAsync(ct);
            var json = JsonSerializer.Serialize(UsageLog.Add(rows, day, purpose, inputTokens, outputTokens), StatsJson.Options);
            if (await Blob.TryUploadAsync(BinaryData.FromString(json), etag, ct))
            {
                return;
            }
        }

        throw new InvalidOperationException("The token usage was not recorded: stats/usage.json kept changing.");
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
        if (await Blob.TryDownloadAsync(ct) is not { } content)
        {
            return ([], null);
        }

        var rows = StatsJson.TryParse<List<UsageDay>>(content.Text)?.Where(r => r is { Day: not null, Purpose: not null }).ToList() ?? [];
        return (rows, content.ETag);
    }
}
