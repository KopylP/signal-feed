namespace PostAgent.Shared.Chat;

public interface IChatStateStore
{
    // Null when there is no state, or when the snapshot cannot be read. An unreadable session only gives a state with Session = null.
    Task<ChatStateHandle?> LoadAsync(CancellationToken ct);

    // Replaces the whole conversation with a new digest. Unconditional on purpose: a digest must win over a turn that is running.
    Task StartNewAsync(ReportSnapshot report, CancellationToken ct);

    // False when the state changed or disappeared since it was loaded.
    Task<bool> TrySaveAsync(ChatStateHandle current, ChatState updated, CancellationToken ct);

    // Deletes the state when its report is older than the cutoff. Returns how many were deleted (0 or 1).
    Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct);
}
