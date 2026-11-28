using Azure.Storage.Blobs;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Embedding.Infrastructure;

public sealed class BlobEmbeddingLock(BlobServiceClient blobs, TimeProvider time, ILogger<BlobEmbeddingLock> log) : IEmbeddingLock
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    // A holder needs well under a second; past this the message fails and the queue retries it later.
    private static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(2);

    public async Task<IAsyncDisposable> AcquireAsync(CancellationToken ct)
    {
        var blob = blobs.GetBlobContainerClient(StorageNames.SettingsContainer).GetBlobClient(StorageNames.EmbeddingLockBlob);
        var deadline = time.GetUtcNow() + MaxWait;
        while (true)
        {
            if (await BlobLock.TryAcquireAsync(blob, time, log, ct) is { } held)
            {
                return held;
            }

            if (time.GetUtcNow() >= deadline)
            {
                throw new TimeoutException($"The embedding lock stayed busy for {MaxWait.TotalMinutes} minutes");
            }

            await Task.Delay(RetryDelay, time, ct);
        }
    }
}
