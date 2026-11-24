using PostAgent.Shared.Posts;

namespace PostAgent.Digest;

public sealed record DigestItem(PostEntity Post, bool IsInteresting, string? Insight = null);
