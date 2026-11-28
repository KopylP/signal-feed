using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using PostAgent.Shared.Infrastructure;
using PostAgent.Shared.Settings;

namespace PostAgent.Shared.TelegramSession;

// The lock of the Telegram session, on a blob next to it: a collection and a sign-in never connect with one auth key at once.
public sealed class BlobSessionLock(
    BlobServiceClient blobs,
    IOptions<AgentOptions> options,
    TimeProvider time,
    ILogger<BlobSessionLock> log) : ISessionLock
{
    public Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken ct) =>
        BlobLock.TryAcquireAsync(
            blobs.GetBlobContainerClient(options.Value.SessionBlobContainer).GetBlobClient(StorageNames.CollectionLockBlob), time, log, ct);
}
