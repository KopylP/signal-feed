using PostAgent.Models;

namespace PostAgent.Services;

/// <summary>
/// Вибирає пости, про які треба сповістити одразу (чиста функція).
/// Лише групи зі списку AlertSources. Канали алертів не викликають.
/// </summary>
public static class AlertSelector
{
    public static IReadOnlyList<PostEntity> Select(IEnumerable<PostEntity> posts, AgentOptions options, DateTimeOffset now)
    {
        var all = posts.ToList();
        var alertSources = options.AlertSourceSet;

        // Джерела, про які вже сповіщали нещодавно.
        var coolingDown = all
            .Where(p => p.AlertedAt >= now.AddHours(-options.AlertCooldownHours))
            .Select(p => p.Source)
            .ToHashSet();

        return all
            .Where(p => p.Status == PostStatus.Scored
                        && p.SourceType == SourceType.Group
                        && alertSources.Contains(p.Source)
                        && !coolingDown.Contains(p.Source)
                        && p.PublishedAt >= now.AddHours(-options.AlertWindowHours)
                        && (p.Score >= options.AlertThreshold || p.MessageCount >= options.BurstMinPosts))
            .GroupBy(p => p.Source)
            .Select(g => g.OrderByDescending(p => p.Score).ThenByDescending(p => p.MessageCount).First())
            .OrderByDescending(p => p.Score)
            .ThenByDescending(p => p.MessageCount)
            .Take(options.AlertMaxPerRun)
            .ToList();
    }
}
