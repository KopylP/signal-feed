namespace PostAgent.Shared.Notifications;

// The chats that receive the digest and alerts, remembered after pairing (ADR-0008, ADR-0014).
public interface IChatBindingStore
{
    // Every bound chat, oldest first.
    Task<IReadOnlyList<long>> GetAllAsync(CancellationToken ct);

    // Adds the chat unless it is bound already or "max" chats are bound.
    Task<BindResult> TryBindAsync(long chatId, int max, CancellationToken ct);
}
