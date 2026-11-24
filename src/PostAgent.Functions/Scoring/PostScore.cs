using System.Text.Json.Serialization;

namespace PostAgent.Scoring;

public sealed record ScoredPosts(List<PostScore> Posts);

public sealed record PostScore(
    string Id,
    int Score,
    string Topic,
    string Reason,
    [property: JsonPropertyName("duplicate_of")] string? DuplicateOf);
