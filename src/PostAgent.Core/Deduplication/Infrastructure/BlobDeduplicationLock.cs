using Azure.Storage.Blobs;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Deduplication.Infrastructure;

public sealed class BlobDeduplicationLock(BlobServiceClient blobs, TimeProvider time, ILogger<BlobDeduplicationLock> log) : IDeduplicationLock
{
    public Task<IAsyncDisposable> AcquireAsync(CancellationToken ct) =>
        BlobLock.AcquireAsync(blobs.GetBlobContainerClient(StorageNames.SettingsContainer).GetBlobClient(StorageNames.DeduplicationLockBlob), time, log, ct);
}
