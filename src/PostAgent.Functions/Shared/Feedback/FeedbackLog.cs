namespace PostAgent.Shared.Feedback;

// The recent votes, oldest first. A new vote on the same post replaces the old one, and only the latest MaxItems are kept,
// so the scoring prompt stays short however long the agent runs.
public static class FeedbackLog
{
    public const int MaxItems = 40;
    public const int MaxTopicChars = 80;

    public static IReadOnlyList<FeedbackItem> Add(IEnumerable<FeedbackItem> current, FeedbackItem item) =>
        current
            .Where(i => i.PostId != item.PostId)
            .Append(item)
            .TakeLast(MaxItems)
            .ToList();
}
