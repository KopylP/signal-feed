using PostAgent.Shared.Posts;

namespace PostAgent.Scoring.Infrastructure;

public sealed record ScoredPosts(List<PostScore> Posts);
