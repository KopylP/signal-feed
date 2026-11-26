using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;

namespace PostAgent.Alerts;

public static class AlertSelector
{
    // One run never floods the chat, however many posts match; the rest are announced by the next runs, while they are still inside the window.
    public const int MaxWatchedPerRun = 5;

    // How far back a followed topic looks: a day (the digest's own window), not the alert window of the groups. Quiet hours or a short AlertWindowHours
    // must not make a post that scored low disappear, and a post is announced only while its status is Scored, so a wide window repeats nothing.
    public static readonly TimeSpan WatchWindow = TimeSpan.FromHours(26);

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

    // Followed topics: a new scored post from any source (channel or group) that matches one, whatever its score and the cooldown of its source.
    // Only posts nobody sent yet (status Scored) are taken, and the alert marks them sent, so a post is announced once.
    // A post that the regular alert took in the same run is left out.
    public static IReadOnlyList<WatchHit> SelectWatched(IEnumerable<PostEntity> posts, AgentOptions options, DateTimeOffset now, IReadOnlySet<string> alreadyAlerted)
    {
        var watches = options.WatchList;
        if (watches.Count == 0)
        {
            return [];
        }

        var since = now - WatchWindow;
        return posts
            .Where(p => p.Status == PostStatus.Scored && p.DuplicateOf is null && p.PublishedAt >= since && !alreadyAlerted.Contains(p.RowKey))
            .Select(p => (Post: p, Phrase: watches.FirstOrDefault(w => WatchMatcher.Matches(p, w))))
            .Where(x => x.Phrase is not null)
            .OrderByDescending(x => x.Post.Score).ThenByDescending(x => x.Post.PublishedAt)
            .Take(MaxWatchedPerRun)
            .Select(x => new WatchHit(x.Post, x.Phrase ?? string.Empty))
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
