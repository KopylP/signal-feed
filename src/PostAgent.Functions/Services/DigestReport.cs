using System.Text.Json;
using PostAgent.Models;

namespace PostAgent.Services;

/// <summary>Пост у дайджесті. Insight — пояснення «що саме цікаве» від моделі (може бути відсутнє).</summary>
public sealed record DigestItem(PostEntity Post, bool IsInteresting, string? Insight = null);

/// <summary>Дайджест до відправки: короткий підсумок дня і пости з поясненнями.</summary>
public sealed record DigestReport(string? Overview, IReadOnlyList<DigestItem> Items)
{
    private sealed record ItemInsight(string Id, string Insight);

    private sealed record Response(string Overview, List<ItemInsight> Items);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Дайджест без підсумку: запасний варіант, якщо модель недоступна.</summary>
    public static DigestReport Plain(IReadOnlyList<DigestItem> items) => new(null, items);

    /// <summary>Додає до постів пояснення з відповіді моделі. Посилання лишаються з наших даних, а не від моделі.</summary>
    public static DigestReport Parse(IReadOnlyList<DigestItem> items, string json)
    {
        var response = JsonSerializer.Deserialize<Response>(json, JsonOptions)
                       ?? throw new JsonException("Model response is empty.");

        var insights = response.Items
            .GroupBy(i => i.Id)
            .ToDictionary(g => g.Key, g => NullIfBlank(g.First().Insight));

        return new DigestReport(
            NullIfBlank(response.Overview),
            items.Select(item => item with { Insight = insights.GetValueOrDefault(item.Post.RowKey) }).ToList());
    }

    private static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
