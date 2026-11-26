using PostAgent.Shared.Stats;
using PostAgent.Stats;

namespace PostAgent.Tests.Stats;

public class StatsReportTests
{
    private static DailyStats Day(string day, int collected, int scored, int interesting, double average, SourceStats[] sources, TopicStats[]? topics = null) =>
        new(day, collected, scored, interesting, 0, average, sources, topics ?? [], []);

    [Fact]
    public void Sums_the_days_and_weights_the_average_by_scored_posts()
    {
        var report = StatsReport.Build(
            [Day("a", 10, 10, 2, 4.0, []), Day("b", 30, 30, 9, 6.0, [])], []);

        Assert.Equal((2, 40, 40, 11), (report.Days, report.Collected, report.Scored, report.Interesting));
        Assert.Equal(5.5, report.AverageScore);
    }

    [Fact]
    public void A_source_needs_enough_posts_to_be_ranked_and_is_ranked_by_its_share_of_interesting_ones()
    {
        var report = StatsReport.Build(
        [
            Day("a", 0, 0, 0, 0, [new("good", 4, 5.0, 4), new("rare", 3, 9.0, 3), new("bad", 6, 2.0, 0)]),
            Day("b", 0, 0, 0, 0, [new("good", 4, 7.0, 3), new("tiny", 2, 9.0, 2)]),
        ], []);

        Assert.Equal(["good", "bad"], report.Best.Select(s => s.Source)); // "rare" has 3 posts, "tiny" 2
        var good = report.Best[0];
        Assert.Equal((8, 7), (good.Posts, good.Interesting));
        Assert.Equal(6.0, good.AverageScore);
        Assert.Equal("bad", report.Weakest[0].Source);
    }

    [Fact]
    public void Topics_are_summed_across_days_most_frequent_first()
    {
        var report = StatsReport.Build(
        [
            Day("a", 0, 0, 0, 0, [], [new("google", 3, 6.0), new("seo", 1, 4.0)]),
            Day("b", 0, 0, 0, 0, [], [new("google", 2, 8.0)]),
        ], []);

        Assert.Equal(("google", 5), (report.Topics[0].Topic, report.Topics[0].Posts));
    }

    [Fact]
    public void Tokens_are_summed_per_purpose()
    {
        var report = StatsReport.Build([], [new("a", "chat", 2, 100, 10), new("b", "chat", 1, 50, 5), new("a", "scoring", 4, 1000, 100)]);

        Assert.Equal(new UsageTotal("chat", 3, 150, 15), report.Usage.Single(u => u.Purpose == "chat"));
        Assert.Equal(2, report.Usage.Count);
    }
}
