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

        var scored = all.Where(p => p.Status != PostStatus.New && p.Score is not null).ToList();
        return new DailyStats(
            day,
            all.Count,
            scored.Count,
            scored.Count(p => p.Score >= interestingThreshold),
            all.Count(p => p.DuplicateOf is not null),
            scored.Count == 0 ? 0 : Math.Round(scored.Average(p => p.Score!.Value), 2),
            scored.GroupBy(p => p.Source)
                .Select(g => new SourceStats(g.Key, g.Count(), Math.Round(g.Average(p => p.Score!.Value), 2), g.Count(p => p.Score >= interestingThreshold)))
                .OrderByDescending(s => s.Posts).ThenBy(s => s.Source, StringComparer.Ordinal)
                .Take(MaxSources).ToList(),
            scored.Where(p => !string.IsNullOrWhiteSpace(p.Topic))
                .GroupBy(p => Normalize(p.Topic!))
                .Select(g => new TopicStats(g.Key, g.Count(), Math.Round(g.Average(p => p.Score!.Value), 2)))
                .OrderByDescending(t => t.Posts).ThenBy(t => t.Topic, StringComparer.Ordinal)
                .Take(MaxTopics).ToList(),
            scored.Where(p => p.DuplicateOf is null)
                .OrderByDescending(p => p.Score).ThenByDescending(p => p.PublishedAt)
                .Take(MaxTop)
                .Select(p => new TopPost(p.RowKey, p.Source, p.Topic is null ? null : Normalize(p.Topic), p.Score!.Value, p.Url))
                .ToList());
    }

    private static string Normalize(string topic)
    {
        var line = string.Join(' ', topic.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
        return line.Length <= MaxTopicChars ? line : line[..MaxTopicChars];
    }
}
