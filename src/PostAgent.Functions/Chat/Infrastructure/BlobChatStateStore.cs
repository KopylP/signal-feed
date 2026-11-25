using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using PostAgent.Shared.Chat;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Chat.Infrastructure;

// One blob per chat: the conversation about the latest digest. Logic that can be tested lives in ChatStateJson.
public sealed class BlobChatStateStore(BlobServiceClient blobs, ILogger<BlobChatStateStore> log) : IChatStateStore
{
    private readonly BlobContainerClient _container = blobs.GetBlobContainerClient(StorageNames.ChatStateContainer);

    private BlobClient BlobOf(long chatId) => _container.GetBlobClient(StorageNames.ChatStateBlobName(chatId));

    public async Task<ChatStateHandle?> LoadAsync(long chatId, CancellationToken ct)
    {
        var raw = await ReadAsync(BlobOf(chatId), ct);
        if (raw is null)
        {
            return null;
        }

        if (ChatStateJson.TryRead(raw.Text, out var state))
        {
            return new ChatStateHandle(state, raw.ETag);
        }

        log.LogWarning("Chat state could not be read and is treated as missing");
        return null;
    }

    public Task StartNewAsync(long chatId, ReportSnapshot report, CancellationToken ct) =>
        BlobOf(chatId).UploadAsync(BinaryData.FromString(ChatStateJson.Write(new ChatState(report, [], null))), overwrite: true, ct);

    public async Task<bool> TryStartAsync(long chatId, ReportSnapshot report, CancellationToken ct)
    {
        var options = new BlobUploadOptions { Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All } };
        try
        {
            await BlobOf(chatId).UploadAsync(BinaryData.FromString(ChatStateJson.Write(new ChatState(report, [], null))), options, ct);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412)
        {
            return false;
        }
    }

    public async Task<bool> TrySaveAsync(long chatId, ChatStateHandle current, ChatState updated, CancellationToken ct)
    {
        var options = new BlobUploadOptions { Conditions = new BlobRequestConditions { IfMatch = new ETag(current.ETag) } };
        try
        {
            await BlobOf(chatId).UploadAsync(BinaryData.FromString(ChatStateJson.Write(updated)), options, ct);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status is 404 or 409 or 412)
        {
            return false;
        }
    }

    public async Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct)
    {
        // Every blob of the container: the one per chat, and the single "current.json" of the version before ADR-0014.
        var deleted = 0;
        await foreach (var item in _container.GetBlobsAsync(cancellationToken: ct))
        {
            deleted += await DeleteIfStaleAsync(_container.GetBlobClient(item.Name), cutoff, ct);
        }

        return deleted;
    }

    private async Task<int> DeleteIfStaleAsync(BlobClient blob, DateTimeOffset cutoff, CancellationToken ct)
    {
        var raw = await ReadAsync(blob, ct);
        if (raw is null)
        {
            return 0;
        }

        // An unreadable blob is judged by when it was last written, so a corrupt one cannot stay forever.
        var created = ChatStateJson.TryRead(raw.Text, out var state) ? state.Report.CreatedAt : raw.LastModified;
        if (created >= cutoff)
        {
            return 0;
        }

        try
        {
            var conditions = new BlobRequestConditions { IfMatch = new ETag(raw.ETag) };
            return (await blob.DeleteIfExistsAsync(conditions: conditions, cancellationToken: ct)).Value ? 1 : 0;
        }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412)
        {
            // A new digest or a turn wrote in between: the state is no longer stale.
            return 0;
        }
    }

    private static async Task<RawBlob?> ReadAsync(BlobClient blob, CancellationToken ct)
    {
        try
        {
            var result = (await blob.DownloadContentAsync(ct)).Value;
            return new RawBlob(result.Content.ToString(), result.Details.ETag.ToString(), result.Details.LastModified);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private sealed record RawBlob(string Text, string ETag, DateTimeOffset LastModified);
}
