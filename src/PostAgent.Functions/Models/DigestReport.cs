
namespace PostAgent.Models;

/// <summary>Пост у дайджесті. Insight — пояснення «що саме цікаве» від моделі (може бути відсутнє).</summary>
public sealed record DigestItem(PostEntity Post, bool IsInteresting, string? Insight = null);

/// <summary>Дайджест до відправки: короткий підсумок дня і пости з поясненнями.</summary>
public sealed record DigestReport(string? Overview, IReadOnlyList<DigestItem> Items)
{
    /// <summary>Дайджест без підсумку: запасний варіант, якщо модель недоступна.</summary>
    public static DigestReport Plain(IReadOnlyList<DigestItem> items) => new(null, items);

    /// <summary>Додає до постів пояснення моделі. Посилання лишаються з наших даних, а не від моделі.</summary>
    public static DigestReport From(IReadOnlyList<DigestItem> items, DigestSummary summary)
    {
        var insights = summary.Items
            .GroupBy(i => i.Id)
            .ToDictionary(g => g.Key, g => NullIfBlank(g.First().Insight));

        return new DigestReport(
            NullIfBlank(summary.Overview),
            items.Select(item => item with { Insight = insights.GetValueOrDefault(item.Post.RowKey) }).ToList());
    }

    private static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
