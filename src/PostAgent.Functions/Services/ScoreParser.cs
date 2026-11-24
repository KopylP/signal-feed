using System.Text.Json;
using PostAgent.Models;

namespace PostAgent.Services;

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
            if (byId.TryGetValue(post.RowKey, out var score))
            {
                post.ApplyScore(score);
            }
            else
            {
                missing.Add(post.RowKey);
            }
        }

        return missing;
    }
}
