using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

namespace PostAgent.Shared.Infrastructure;

// A lock shared by every instance: a lease on an empty blob, one per protected resource. The lease is finite and renewed while it is
// held, so a dead instance blocks the next one for at most a minute.
public static class BlobLock
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RenewInterval = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    // A holder of a waited-for lock needs well under a second; past this the caller fails and its queue retries later.
    private static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(2);

    // Waits while someone else holds it, for work that must not be skipped (a queue message).
    public static async Task<IAsyncDisposable> AcquireAsync(BlobClient blob, TimeProvider time, ILogger log, CancellationToken ct)
    {
        var deadline = time.GetUtcNow() + MaxWait;
        while (true)
        {
            if (await TryAcquireAsync(blob, time, log, ct) is { } held)
            {
                return held;
            }

            if (time.GetUtcNow() >= deadline)
            {
                throw new TimeoutException($"The lock {blob.Name} stayed busy for {MaxWait.TotalMinutes} minutes");
            }

            await Task.Delay(RetryDelay, time, ct);
        }
    }

    // Null when someone else holds it. Disposing the handle releases it.
    public static async Task<IAsyncDisposable?> TryAcquireAsync(BlobClient blob, TimeProvider time, ILogger log, CancellationToken ct)
    {
        try
        {
            await blob.UploadAsync(BinaryData.Empty, overwrite: false, ct);
        }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412)
        {
            // The lock blob already exists (and may be leased right now).
        }

        var lease = blob.GetBlobLeaseClient();
        try
        {
            await lease.AcquireAsync(LeaseDuration, cancellationToken: ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            return null;
        }

        return new RenewedBlobLease(lease, blob.Name, RenewInterval, time, log);
    }
}
