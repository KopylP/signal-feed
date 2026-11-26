namespace PostAgent.Shared.Feedback;

// One like or dislike the reader gave to a post through the chat agent. The topic is the scoring topic or the start of the post (channel text), so it is data.
public sealed record FeedbackItem(string PostId, string Source, string Topic, FeedbackVote Vote, DateTimeOffset At);
