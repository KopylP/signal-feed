namespace PostAgent.Shared.Notifications;

// The chat that receives the digest and alerts, remembered after pairing (ADR-0008).
public interface IChatBindingStore
{
    Task<long?> GetAsync(CancellationToken ct);

    // False when a chat is already bound: the first pairing wins.
    Task<bool> TryBindAsync(long chatId, CancellationToken ct);
}
