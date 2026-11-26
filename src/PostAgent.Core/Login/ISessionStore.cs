namespace PostAgent.Login;

// Where the collector finds its Telegram session.
public interface ISessionStore
{
    Task SaveAsync(byte[] session, CancellationToken ct);
}
