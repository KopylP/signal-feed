using PostAgent.Shared.Posts;
using PostAgent.Shared.Stats;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Shared.Stats;

public class DailyStatsBuilderTests
{
    private static PostEntity P(string key, int? score, string source = "src", string? topic = "Google", string? duplicateOf = null, PostStatus status = PostStatus.Scored)
    {
        var post = Post(key, score: score, source: source, duplicateOf: duplicateOf, status: status);
        post.Topic = topic;
        return post;
    }

    [Fact]
    public void A_day_without_posts_has_no_summary() =>
        Assert.Null(DailyStatsBuilder.Build("2026-11-23", [], 7));

    [Fact]
    public void Counts_what_was_collected_scored_interesting_and_duplicated()
    {
        var stats = DailyStatsBuilder.Build("2026-11-23",
            [P("a", 9), P("b", 7), P("c", 3), P("d", 8, duplicateOf: "a"), Unscored("e")], 7)!;

        Assert.Equal((5, 4, 3, 1), (stats.Collected, stats.Scored, stats.Interesting, stats.Duplicates));
        Assert.Equal(6.75, stats.AverageScore);
    }

    [Fact]
    public void Sums_a_source_and_counts_its_interesting_posts()
    {
        var stats = DailyStatsBuilder.Build("d", [P("a", 9, "x"), P("b", 3, "x"), P("c", 8, "y")], 7)!;

        var x = Assert.Single(stats.Sources, s => s.Source == "x");
        Assert.Equal((2, 6.0, 1), (x.Posts, x.AverageScore, x.Interesting));
        Assert.Equal("x", stats.Sources[0].Source); // the busiest first
    }

    [Fact]
    public void Topics_are_normalized_counted_and_cut()
    {
        var stats = DailyStatsBuilder.Build("d", [P("a", 9, topic: "  Google   Update "), P("b", 5, topic: "google update"), P("c", 5, topic: new string('я', 200)), P("d", 5, topic: null)], 7)!;

        var google = Assert.Single(stats.Topics, t => t.Topic == "google update");
        Assert.Equal((2, 7.0), (google.Posts, google.AverageScore));
        Assert.All(stats.Topics, t => Assert.True(t.Topic.Length <= 60));
        Assert.Equal(2, stats.Topics.Count);
    }

    [Fact]
    public void The_best_posts_are_the_highest_scored_and_never_a_duplicate_and_carry_no_text()
    {
        var stats = DailyStatsBuilder.Build("d", Enumerable.Range(1, 8).Select(i => P("p" + i, i)).Append(P("dup", 10, duplicateOf: "p8")), 7)!;

        Assert.Equal(DailyStatsBuilder.MaxTop, stats.Top.Count);
        Assert.Equal(["p8", "p7", "p6", "p5", "p4"], stats.Top.Select(t => t.PostId));
        Assert.DoesNotContain(stats.Top, t => t.PostId == "dup");
    }

    [Fact]
    public void Lists_are_capped()
    {
        var posts = Enumerable.Range(1, 60).Select(i => P("p" + i, 5, source: "s" + i, topic: "t" + i));

        var stats = DailyStatsBuilder.Build("d", posts, 7)!;

        Assert.Equal(DailyStatsBuilder.MaxSources, stats.Sources.Count);
        Assert.Equal(DailyStatsBuilder.MaxTopics, stats.Topics.Count);
    }

    [Fact]
    public void A_damaged_blob_reads_as_nothing_and_an_empty_one_as_an_empty_list()
    {
        Assert.Null(StatsJson.TryParse<DailyStats>("not json"));
        Assert.Null(StatsJson.TryParse<List<UsageDay>>("{\"a\":1}"));
        Assert.Empty(StatsJson.TryParse<List<UsageDay>>("[]")!);
    }
}
