using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using PostAgent.Shared.Chat;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Chat.Infrastructure;

// One blob for the single bound chat: the conversation about the latest digest. Logic that can be tested lives in ChatStateJson.
public sealed class BlobChatStateStore(BlobServiceClient blobs, ILogger<BlobChatStateStore> log) : IChatStateStore
{
    private readonly BlobClient _blob = blobs
        .GetBlobContainerClient(StorageNames.ChatStateContainer)
        .GetBlobClient(StorageNames.ChatStateBlob);

    public async Task<ChatStateHandle?> LoadAsync(CancellationToken ct)
    {
        var raw = await ReadAsync(ct);
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

    public Task StartNewAsync(ReportSnapshot report, CancellationToken ct) =>
        _blob.UploadAsync(BinaryData.FromString(ChatStateJson.Write(new ChatState(report, [], null))), overwrite: true, ct);

    public async Task<bool> TrySaveAsync(ChatStateHandle current, ChatState updated, CancellationToken ct)
    {
        var options = new BlobUploadOptions { Conditions = new BlobRequestConditions { IfMatch = new ETag(current.ETag) } };
        try
        {
            await _blob.UploadAsync(BinaryData.FromString(ChatStateJson.Write(updated)), options, ct);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status is 404 or 409 or 412)
        {
            return false;
        }
    }

    public async Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct)
    {
        var raw = await ReadAsync(ct);
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
            return (await _blob.DeleteIfExistsAsync(conditions: conditions, cancellationToken: ct)).Value ? 1 : 0;
        }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412)
        {
            // A new digest or a turn wrote in between: the state is no longer stale.
            return 0;
        }
    }

    private async Task<RawBlob?> ReadAsync(CancellationToken ct)
    {
        try
        {
            var result = (await _blob.DownloadContentAsync(ct)).Value;
            return new RawBlob(result.Content.ToString(), result.Details.ETag.ToString(), result.Details.LastModified);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private sealed record RawBlob(string Text, string ETag, DateTimeOffset LastModified);
}
