using Azure.Storage.Blobs;
using PostAgent.Shared.Infrastructure;

namespace PostAgent.Alerts.Infrastructure;

public sealed class BlobAlertDecisionLock(BlobServiceClient blobs, TimeProvider time, ILogger<BlobAlertDecisionLock> log) : IAlertDecisionLock
{
    public Task<IAsyncDisposable> AcquireAsync(CancellationToken ct) =>
        BlobLock.AcquireAsync(blobs.GetBlobContainerClient(StorageNames.SettingsContainer).GetBlobClient(StorageNames.AlertDecisionLockBlob), time, log, ct);
}
