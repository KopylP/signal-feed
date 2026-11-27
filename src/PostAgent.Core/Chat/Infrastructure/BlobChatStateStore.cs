using Azure;
using Azure.Storage.Blobs;
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
        if (await BlobOf(chatId).TryDownloadAsync(ct) is not { } content)
        {
            return null;
        }

        if (ChatStateJson.TryRead(content.Text, out var state))
        {
            return new ChatStateHandle(state, content.ETag.ToString());
        }

        log.LogWarning("Chat state could not be read and is treated as missing");
        return null;
    }

    public Task StartNewAsync(long chatId, ReportSnapshot report, CancellationToken ct) =>
        BlobOf(chatId).UploadAsync(Empty(report), overwrite: true, ct);

    public Task<bool> TryStartAsync(long chatId, ReportSnapshot report, CancellationToken ct) =>
        BlobOf(chatId).TryUploadAsync(Empty(report), ifMatch: null, ct);

    public Task<bool> TrySaveAsync(long chatId, ChatStateHandle current, ChatState updated, CancellationToken ct) =>
        BlobOf(chatId).TryUploadAsync(BinaryData.FromString(ChatStateJson.Write(updated)), new ETag(current.ETag), ct);

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

    private static async Task<int> DeleteIfStaleAsync(BlobClient blob, DateTimeOffset cutoff, CancellationToken ct)
    {
        if (await blob.TryDownloadAsync(ct) is not { } content)
        {
            return 0;
        }

        // An unreadable blob is judged by when it was last written, so a corrupt one cannot stay forever.
        var created = ChatStateJson.TryRead(content.Text, out var state) ? state.Report.CreatedAt : content.LastModified;

        // A new digest or a turn that wrote in between makes the delete fail: the state is no longer stale.
        return created < cutoff && await blob.TryDeleteAsync(content.ETag, ct) ? 1 : 0;
    }

    private static BinaryData Empty(ReportSnapshot report) => BinaryData.FromString(ChatStateJson.Write(new ChatState(report, [], null)));
}
