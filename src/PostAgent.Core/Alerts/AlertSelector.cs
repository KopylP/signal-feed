using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;

namespace PostAgent.Alerts;

// What deserves an alert (no I/O). A post already decided (AlertedAt or WatchAlertedAt) or sent is never chosen again.
public static class AlertSelector
{
    // One decision never floods the chat; the rest of the matches go to the digest.
    public const int MaxWatchedPerDecision = 5;

    // How far back a followed topic looks: a day, the digest's own window, also for a topic that was just added.
    public static readonly TimeSpan WatchWindow = TimeSpan.FromHours(26);

    // Important or bursting posts of the groups in AlertSources, the best one per group, groups in cooldown left out.
    // recent holds the posts of the last AlertWindowHours + AlertCooldownHours, whose AlertedAt tells which groups are cooling down.
    public static IReadOnlyList<PostEntity> SelectGroup(IEnumerable<PostEntity> candidates, IEnumerable<PostEntity> recent, AgentOptions options, DateTimeOffset now)
    {
        var alertSources = options.AlertSourceSet;
        var windowStart = now.AddHours(-options.AlertWindowHours);
        var coolingDown = recent.Where(p => p.AlertedAt >= now.AddHours(-options.AlertCooldownHours)).Select(p => p.Source).ToHashSet();

        var bestPerSource = candidates
            .Where(p => IsUndecided(p) && p.SourceType == SourceType.Group && alertSources.Contains(p.Source) && p.PublishedAt >= windowStart)
            .Where(p => p.Score >= options.AlertThreshold || p.MessageCount >= options.BurstMinPosts)
            .Where(p => !coolingDown.Contains(p.Source))
            .GroupBy(p => p.Source)
            .Select(group => Rank(group).First());

        return Rank(bestPerSource).Take(options.AlertMaxPerRun).ToList();
    }

    // Posts from any source that match a followed topic, whatever their score and the cooldown of their source: by meaning (the post's
    // vector against the topic's, ADR-0023) or by the words of WatchMatcher, which also covers a post or a topic without a vector.
    public static IReadOnlyList<WatchHit> SelectWatched(IEnumerable<PostEntity> candidates, IReadOnlyList<string> topics,
        IReadOnlyDictionary<string, float[]> vectors, AgentOptions options, DateTimeOffset now)
    {
        if (topics.Count == 0)
        {
            return [];
        }

        return candidates
            .Where(p => IsUndecided(p) && p.DuplicateOf is null && p.PublishedAt >= now - WatchWindow)
            .Select(p => (Post: p, Topic: topics.FirstOrDefault(t => WatchMatcher.Matches(p, t)
                || PostVectors.Similarity(p.Embedding, vectors.GetValueOrDefault(t)) >= options.WatchSimilarityThreshold)))
            .Where(x => x.Topic is not null)
            .OrderByDescending(x => x.Post.Score).ThenByDescending(x => x.Post.PublishedAt)
            .Take(MaxWatchedPerDecision)
            .Select(x => new WatchHit(x.Post, x.Topic ?? string.Empty))
            .ToList();
    }

    private static bool IsUndecided(PostEntity post) =>
        post.Status == PostStatus.Scored && post.Score is not null && post.AlertedAt is null && post.WatchAlertedAt is null;

    private static IOrderedEnumerable<PostEntity> Rank(IEnumerable<PostEntity> posts) =>
        posts.OrderByDescending(p => p.Score).ThenByDescending(p => p.MessageCount);
}
