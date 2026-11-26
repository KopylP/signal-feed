namespace PostAgent.Shared.TelegramSession;

// Keeps two users of the Telegram session apart (a collection and the timer, or a collection and a sign-in from the bot):
// two connections on one auth key can make Telegram revoke the session, and a collection that flushes the old session would overwrite a new one.
public interface ISessionLock
{
    // Null when someone else holds the lock. Disposing the handle releases it.
    Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken ct);
}
