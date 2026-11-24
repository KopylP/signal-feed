using System.Text.Json;
using System.Text.Json.Serialization;
using PostAgent.Models;

namespace PostAgent.Services;

public sealed record PostScore(
    string Id,
    int Score,
    string Topic,
    string Reason,
    [property: JsonPropertyName("duplicate_of")] string? DuplicateOf);

/// <summary>Розбір відповіді моделі та застосування оцінок до постів.</summary>
public static class ScoreParser
{
    private sealed record Response(List<PostScore> Posts);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<PostScore> Parse(string json) =>
        JsonSerializer.Deserialize<Response>(json, JsonOptions)?.Posts
        ?? throw new JsonException("Model response has no 'posts' array.");

    /// <summary>Записує оцінки в пости. Повертає id постів, для яких оцінки немає.</summary>
    public static IReadOnlyList<string> Apply(IEnumerable<PostEntity> posts, IReadOnlyList<PostScore> scores)
    {
        var byId = scores.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());
        var missing = new List<string>();

        foreach (var post in posts)
        {
            if (!byId.TryGetValue(post.RowKey, out var score))
            {
                missing.Add(post.RowKey);
                continue;
            }

            post.Score = Math.Clamp(score.Score, 0, 10);
            post.Topic = score.Topic;
            post.Reason = score.Reason;
            post.DuplicateOf = string.IsNullOrWhiteSpace(score.DuplicateOf) || score.DuplicateOf == post.RowKey
                ? null
                : score.DuplicateOf;
            post.Status = PostStatus.Scored;
        }

        return missing;
    }
}
