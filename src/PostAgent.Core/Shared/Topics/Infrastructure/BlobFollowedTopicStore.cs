using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Shared.Topics.Infrastructure;

public sealed class BlobFollowedTopicStore(BlobServiceClient blobs, ILogger<BlobFollowedTopicStore> log) : IFollowedTopicStore
{
    private const int Attempts = 3;

    private BlobClient Blob => blobs
        .GetBlobContainerClient(StorageNames.SettingsContainer)
        .GetBlobClient(StorageNames.FollowedTopicsBlob);

    public async Task<IReadOnlyDictionary<string, float[]>> LoadAsync(CancellationToken ct) => (await ReadAsync(ct)).Topics;

    public async Task SaveAsync(string phrase, float[] vector, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            var (topics, etag) = await ReadAsync(ct);
            var next = topics.Where(t => !string.Equals(t.Key, phrase, StringComparison.OrdinalIgnoreCase))
                .Select(t => new StoredTopic(t.Key, t.Value))
                .Append(new StoredTopic(phrase, vector))
                .ToList();
            if (await Blob.TryUploadAsync(BinaryData.FromString(JsonSerializer.Serialize(next)), etag, ct))
            {
                return;
            }
        }

        throw new InvalidOperationException("The topic vector was not saved: settings/followed-topics.json kept changing.");
    }

    private async Task<(Dictionary<string, float[]> Topics, ETag? ETag)> ReadAsync(CancellationToken ct)
    {
        var topics = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        if (await Blob.TryDownloadAsync(ct) is not { } content)
        {
            return (topics, null);
        }

        try
        {
            foreach (var topic in JsonSerializer.Deserialize<List<StoredTopic>>(content.Text) ?? [])
            {
                topics[topic.Phrase] = topic.Vector;
            }
        }
        catch (JsonException ex)
        {
            // The ETag stays, so the next save replaces the damaged file; meanwhile the topics match by words.
            log.LogWarning(ex, "settings/followed-topics.json could not be read, the next saved topic replaces it");
        }

        return (topics, content.ETag);
    }

    private sealed record StoredTopic(string Phrase, float[] Vector);
}
