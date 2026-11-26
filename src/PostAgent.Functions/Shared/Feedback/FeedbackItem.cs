namespace PostAgent.Shared.Feedback;

// One 👍 or 👎 the reader gave to a digest entry. The topic is the digest title (model text over channel text), so it is data.
public sealed record FeedbackItem(string PostId, string Source, string Topic, FeedbackVote Vote, DateTimeOffset At);
