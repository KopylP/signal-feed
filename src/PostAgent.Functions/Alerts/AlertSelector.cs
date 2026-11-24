using PostAgent.Posts;
using PostAgent.Settings;

namespace PostAgent.Alerts;

public static class AlertSelector
{
    public static DateTimeOffset LookbackStart(AgentOptions options, DateTimeOffset now) =>
        now.AddHours(-(options.AlertWindowHours + options.AlertCooldownHours));

    public static IReadOnlyList<PostEntity> Select(IEnumerable<PostEntity> posts, AgentOptions options, DateTimeOffset now)
    {
        var all = posts.ToList();
        var alertSources = options.AlertSourceSet;
        var windowStart = now.AddHours(-options.AlertWindowHours);
        var coolingDown = SourcesAlertedSince(all, now.AddHours(-options.AlertCooldownHours));

        var bestPerSource = all
            .Where(p => IsCandidate(p, alertSources, windowStart) && IsAlertWorthy(p, options))
            .Where(p => !coolingDown.Contains(p.Source))
            .GroupBy(p => p.Source)
            .Select(group => Rank(group).First());

        return Rank(bestPerSource)
            .Take(options.AlertMaxPerRun)
            .ToList();
    }

    private static HashSet<string> SourcesAlertedSince(IEnumerable<PostEntity> posts, DateTimeOffset since) =>
        posts.Where(p => p.AlertedAt >= since).Select(p => p.Source).ToHashSet();

    private static bool IsCandidate(PostEntity post, IReadOnlySet<string> alertSources, DateTimeOffset windowStart) =>
        post.Status == PostStatus.Scored
        && post.SourceType == SourceType.Group
        && alertSources.Contains(post.Source)
        && post.PublishedAt >= windowStart;

    private static bool IsAlertWorthy(PostEntity post, AgentOptions options) =>
        post.Score >= options.AlertThreshold || post.MessageCount >= options.BurstMinPosts;

    private static IOrderedEnumerable<PostEntity> Rank(IEnumerable<PostEntity> posts) =>
        posts.OrderByDescending(p => p.Score).ThenByDescending(p => p.MessageCount);
}
