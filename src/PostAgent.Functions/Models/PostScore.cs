using System.Text.Json.Serialization;

namespace PostAgent.Models;

/// <summary>Відповідь моделі на оцінювання: схему для Structured Outputs Microsoft.Extensions.AI будує з цього типу.</summary>
public sealed record ScoredPosts(List<PostScore> Posts);

/// <summary>Оцінка одного посту.</summary>
public sealed record PostScore(
    string Id,
    int Score,
    string Topic,
    string Reason,
    [property: JsonPropertyName("duplicate_of")] string? DuplicateOf);
