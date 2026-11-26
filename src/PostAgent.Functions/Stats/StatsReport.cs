using PostAgent.Shared.Stats;

namespace PostAgent.Stats;

// The numbers behind /stats, summed over the days asked for (pure).
public sealed record StatsReport(
    int Days,
    int Collected,
    int Scored,
    int Interesting,
    double AverageScore,
    IReadOnlyList<SourceRank> Best,
    IReadOnlyList<SourceRank> Weakest,
    IReadOnlyList<TopicStats> Topics,
    IReadOnlyList<UsageTotal> Usage)
{
    // A source needs this many scored posts before it is ranked: three posts say nothing about it.
    public const int MinPostsToRank = 5;
    public const int ListSize = 5;

    public static StatsReport Build(IReadOnlyList<DailyStats> days, IReadOnlyList<UsageDay> usage)
    {
        var scored = days.Sum(d => d.Scored);
        var sources = days.SelectMany(d => d.Sources)
            .GroupBy(s => s.Source)
            .Select(g => new SourceRank(g.Key, g.Sum(s => s.Posts), g.Sum(s => s.Interesting), g.Sum(s => s.AverageScore * s.Posts) / Math.Max(1, g.Sum(s => s.Posts))))
            .Where(s => s.Posts >= MinPostsToRank)
            .ToList();

        return new StatsReport(
            days.Count,
            days.Sum(d => d.Collected),
            scored,
            days.Sum(d => d.Interesting),
            scored == 0 ? 0 : days.Sum(d => d.AverageScore * d.Scored) / scored,
            sources.OrderByDescending(s => s.Share).ThenByDescending(s => s.Posts).ThenBy(s => s.Source, StringComparer.Ordinal).Take(ListSize).ToList(),
            sources.OrderBy(s => s.Share).ThenByDescending(s => s.Posts).ThenBy(s => s.Source, StringComparer.Ordinal).Take(ListSize).ToList(),
            days.SelectMany(d => d.Topics)
                .GroupBy(t => t.Topic)
                .Select(g => new TopicStats(g.Key, g.Sum(t => t.Posts), g.Sum(t => t.AverageScore * t.Posts) / Math.Max(1, g.Sum(t => t.Posts))))
                .OrderByDescending(t => t.Posts).ThenBy(t => t.Topic, StringComparer.Ordinal).Take(ListSize).ToList(),
            usage.GroupBy(u => u.Purpose)
                .Select(g => new UsageTotal(g.Key, g.Sum(u => u.Calls), g.Sum(u => u.InputTokens), g.Sum(u => u.OutputTokens)))
                .OrderBy(u => u.Purpose, StringComparer.Ordinal).ToList());
    }
}

public sealed record SourceRank(string Source, int Posts, int Interesting, double AverageScore)
{
    // The share of its scored posts that were interesting: what makes a source worth following.
    public double Share => Posts == 0 ? 0 : (double)Interesting / Posts;
}

public sealed record UsageTotal(string Purpose, int Calls, long InputTokens, long OutputTokens);
