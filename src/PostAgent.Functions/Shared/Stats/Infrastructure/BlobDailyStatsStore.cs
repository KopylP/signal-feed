using System.Globalization;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Shared.Stats.Infrastructure;

public sealed class BlobDailyStatsStore(BlobServiceClient blobs) : IDailyStatsStore
{
    private BlobContainerClient Container => blobs.GetBlobContainerClient(StorageNames.SettingsContainer);

    public Task SaveAsync(DailyStats stats, CancellationToken ct) =>
        Container.GetBlobClient(StorageNames.DailyStatsBlobName(stats.Day))
            .UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(stats, StatsJson.Options)), overwrite: true, ct);

    public async Task<IReadOnlyList<DailyStats>> LoadAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var days = new List<DailyStats>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            try
            {
                var content = (await Container.GetBlobClient(StorageNames.DailyStatsBlobName(Name(day))).DownloadContentAsync(ct)).Value.Content.ToString();
                if (StatsJson.TryParse<DailyStats>(content) is { Day: not null } stats)
                {
                    days.Add(stats);
                }
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
            }
        }

        return days;
    }

    public async Task<int> DeleteOlderThanAsync(DateOnly cutoff, CancellationToken ct)
    {
        var deleted = 0;
        await foreach (var item in Container.GetBlobsAsync(BlobTraits.None, BlobStates.None, StorageNames.DailyStatsPrefix, ct))
        {
            var name = Path.GetFileNameWithoutExtension(item.Name);
            if (DateOnly.TryParseExact(name, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) && day < cutoff)
            {
                deleted += (await Container.DeleteBlobIfExistsAsync(item.Name, cancellationToken: ct)).Value ? 1 : 0;
            }
        }

        return deleted;
    }

    private static string Name(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
