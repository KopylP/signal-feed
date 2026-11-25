using Azure.Storage.Blobs.Specialized;

namespace PostAgent.Shared.TelegramSession;

// A held blob lease that renews itself until it is disposed, then releases it.
public sealed class RenewedBlobLease : IAsyncDisposable
{
    private readonly BlobLeaseClient _lease;
    private readonly ILogger _log;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _renewing;

    public RenewedBlobLease(BlobLeaseClient lease, TimeSpan renewInterval, TimeProvider time, ILogger log)
    {
        _lease = lease;
        _log = log;
        _renewing = RenewAsync(renewInterval, time, _stop.Token);
    }

    private async Task RenewAsync(TimeSpan interval, TimeProvider time, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(interval, time);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    await _lease.RenewAsync(cancellationToken: ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // The work goes on: the lease still has time left, and the next renewal may succeed.
                    _log.LogWarning(ex, "Could not renew the Telegram session lock");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Disposed: the work is over.
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        await _renewing;
        _stop.Dispose();

        try
        {
            await _lease.ReleaseAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Could not release the Telegram session lock, it expires on its own");
        }
    }
}
