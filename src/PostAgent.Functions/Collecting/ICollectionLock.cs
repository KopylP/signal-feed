namespace PostAgent.Collecting;

// Keeps two collections from using the Telegram session at once (a run-now collect and the timer):
// two connections on one auth key can make Telegram revoke the session.
public interface ICollectionLock
{
    // Null when another collection holds the lock. Disposing the handle releases it.
    Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken ct);
}
