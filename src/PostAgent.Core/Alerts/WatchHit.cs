using PostAgent.Shared.Posts;

namespace PostAgent.Alerts;

// A new post that matched a followed topic, and which topic.
public sealed record WatchHit(PostEntity Post, string Phrase);
