using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Options;
using PostAgent.Shared.Infrastructure;
using PostAgent.Shared.Settings;

namespace PostAgent.Shared.TelegramSession;

// A lease on a lock blob next to the session. A finite lease that is renewed while the run lasts:
// if the instance dies, the lease expires on its own and the next user is not blocked for good.
public sealed class BlobSessionLock(
    BlobServiceClient blobs,
    IOptions<AgentOptions> options,
    TimeProvider time,
    ILogger<BlobSessionLock> log) : ISessionLock
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RenewInterval = TimeSpan.FromSeconds(20);

    public async Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken ct)
    {
        var blob = blobs
            .GetBlobContainerClient(options.Value.SessionBlobContainer)
            .GetBlobClient(StorageNames.CollectionLockBlob);
        await CreateIfMissingAsync(blob, ct);

        var lease = blob.GetBlobLeaseClient();
        try
        {
            await lease.AcquireAsync(LeaseDuration, cancellationToken: ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            return null; // a collection or a sign-in holds the lease
        }

        return new RenewedBlobLease(lease, RenewInterval, time, log);
    }

    private static async Task CreateIfMissingAsync(BlobClient blob, CancellationToken ct)
    {
        try
        {
            await blob.UploadAsync(BinaryData.Empty, overwrite: false, ct);
        }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412)
        {
            // The lock blob already exists (and may be leased right now).
        }
    }
}
