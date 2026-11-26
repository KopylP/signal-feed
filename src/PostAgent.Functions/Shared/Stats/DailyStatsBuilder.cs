using PostAgent.Shared.Posts;

namespace PostAgent.Shared.Stats;

public static class DailyStatsBuilder
{
    public const int MaxSources = 30;
    public const int MaxTopics = 25;
    public const int MaxTop = 5;
    private const int MaxTopicChars = 60;

    // Null when the day has no posts. interestingThreshold is AgentOptions.InterestingThreshold: the score from which a post is "interesting".
    public static DailyStats? Build(string day, IEnumerable<PostEntity> posts, int interestingThreshold)
    {
        var all = posts.ToList();
        if (all.Count == 0)
        {
            return null;
        }

        // Scored posts with their score read once, so nothing below needs to assume it is there.
        var scored = all
            .Where(p => p.Status != PostStatus.New)
            .Select(p => (Post: p, Score: p.Score))
            .Where(x => x.Score.HasValue)
            .Select(x => (x.Post, Score: x.Score.GetValueOrDefault()))
            .ToList();

        return new DailyStats(
            day,
            all.Count,
            scored.Count,
            scored.Count(x => x.Score >= interestingThreshold),
            all.Count(p => p.DuplicateOf is not null),
            scored.Count == 0 ? 0 : Math.Round(scored.Average(x => x.Score), 2),
            scored.GroupBy(x => x.Post.Source)
                .Select(g => new SourceStats(g.Key, g.Count(), Math.Round(g.Average(x => x.Score), 2), g.Count(x => x.Score >= interestingThreshold)))
                .OrderByDescending(s => s.Posts).ThenBy(s => s.Source, StringComparer.Ordinal)
                .Take(MaxSources).ToList(),
            scored.Select(x => (x.Score, Topic: Normalize(x.Post.Topic)))
                .Where(x => x.Topic.Length > 0)
                .GroupBy(x => x.Topic)
                .Select(g => new TopicStats(g.Key, g.Count(), Math.Round(g.Average(x => x.Score), 2)))
                .OrderByDescending(t => t.Posts).ThenBy(t => t.Topic, StringComparer.Ordinal)
                .Take(MaxTopics).ToList(),
            scored.Where(x => x.Post.DuplicateOf is null)
                .OrderByDescending(x => x.Score).ThenByDescending(x => x.Post.PublishedAt)
                .Take(MaxTop)
                .Select(x => new TopPost(x.Post.RowKey, x.Post.Source, x.Post.Topic is null ? null : Normalize(x.Post.Topic), x.Score, x.Post.Url))
                .ToList());
    }

    // One line, lower case, cut; an empty string for no topic.
    private static string Normalize(string? topic)
    {
        var line = string.Join(' ', (topic ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
        return line.Length <= MaxTopicChars ? line : line[..MaxTopicChars];
    }
}
