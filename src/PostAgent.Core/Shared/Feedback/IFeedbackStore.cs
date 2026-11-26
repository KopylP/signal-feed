namespace PostAgent.Shared.Feedback;

// The votes of every bound chat in one blob (ADR-0014: the chats share the settings, and the scoring is one for all).
public interface IFeedbackStore
{
    // Oldest first; empty when nothing was voted yet.
    Task<IReadOnlyList<FeedbackItem>> LoadAsync(CancellationToken ct);

    // Replaces an earlier vote on the same post and keeps the latest FeedbackLog.MaxItems. Retries a lost race on its own.
    Task RecordAsync(FeedbackItem item, CancellationToken ct);
}
