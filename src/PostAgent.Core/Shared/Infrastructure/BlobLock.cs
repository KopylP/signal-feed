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
