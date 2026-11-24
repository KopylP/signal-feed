using PostAgent.Shared.Posts;

namespace PostAgent.Digest;

// One line of the digest. The post is ours (channel, date, link come from it); the texts come from the model.
public sealed record DigestEntry(PostEntity Post, string? Title, string Text, string? Why = null);
