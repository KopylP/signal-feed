namespace PostAgent.Shared.Chat;

// One conversation per chat (ADR-0014): every method works on the conversation of the chat it is given.
public interface IChatStateStore
{
    // Null when there is no state, or when the snapshot cannot be read. An unreadable session only gives a state with Session = null.
    Task<ChatStateHandle?> LoadAsync(long chatId, CancellationToken ct);

    // Replaces the whole conversation of the chat with a new digest. Unconditional on purpose: a digest must win over a turn that is running.
    Task StartNewAsync(long chatId, ReportSnapshot report, CancellationToken ct);

    // Creates the conversation of a chat that has none yet (no digest was sent). False when one already exists, so a digest that arrived first is never replaced.
    Task<bool> TryStartAsync(long chatId, ReportSnapshot report, CancellationToken ct);

    // False when the state changed or disappeared since it was loaded.
    Task<bool> TrySaveAsync(long chatId, ChatStateHandle current, ChatState updated, CancellationToken ct);

    // Deletes every conversation whose report is older than the cutoff. Returns how many were deleted.
    Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct);
}
