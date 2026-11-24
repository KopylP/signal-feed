using static PostAgent.Tests.TestData;
using PostAgent.Digest;
using PostAgent.Shared.Posts;

namespace PostAgent.Tests.Digest;

public class DigestBuilderTests
{
    [Fact]
    public void Normal_day_takes_best_posts_sorted_by_score_then_newer_first()
    {
        var posts = new[]
        {
            Post("a", score: 9, hoursAgo: 5),
            Post("b", score: 8, hoursAgo: 4),
            Post("c", score: 8, hoursAgo: 2),
            Post("d", score: 7, hoursAgo: 3),
            Post("e", score: 3, hoursAgo: 1),
        };

        var items = DigestBuilder.Build(posts, Options(o => o.DigestSize = 3), Now);

        Assert.Equal(["a", "c", "b"], items.Select(i => i.Post.RowKey));
        Assert.All(items, i => Assert.True(i.IsInteresting));
    }

    [Fact]
    public void Day_without_posts_above_threshold_still_returns_the_best_marked_as_not_interesting()
    {
        var posts = new[] { Post("a", score: 5), Post("b", score: 6) };

        var items = DigestBuilder.Build(posts, Options(o => o.InterestingThreshold = 7), Now);

        Assert.Equal(["b", "a"], items.Select(i => i.Post.RowKey));
        Assert.All(items, i => Assert.False(i.IsInteresting));
    }

    [Fact]
    public void Score_equal_to_threshold_is_interesting()
    {
        var items = DigestBuilder.Build([Post("a", score: 7)], Options(o => o.InterestingThreshold = 7), Now);
        Assert.True(items[0].IsInteresting);
    }

    [Fact]
    public void Day_without_any_posts_gives_empty_digest()
    {
        Assert.Empty(DigestBuilder.Build([], Options(), Now));
    }

    [Fact]
    public void Duplicates_are_dropped()
    {
        var posts = new[] { Post("a", score: 9), Post("b", score: 4, duplicateOf: "a") };

        var items = DigestBuilder.Build(posts, Options(), Now);

        Assert.Equal(["a"], items.Select(i => i.Post.RowKey));
    }

    [Fact]
    public void Falls_back_to_48_hours_when_nothing_in_the_last_24()
    {
        var posts = new[] { Post("old", score: 8, hoursAgo: 30), Post("older", score: 8, hoursAgo: 60) };

        var items = DigestBuilder.Build(posts, Options(), Now);

        Assert.Equal(["old"], items.Select(i => i.Post.RowKey));
    }

    [Fact]
    public void Does_not_use_48_hours_when_there_are_posts_in_the_last_24()
    {
        var posts = new[] { Post("fresh", score: 2, hoursAgo: 2), Post("old", score: 9, hoursAgo: 30) };

        var items = DigestBuilder.Build(posts, Options(), Now);

        Assert.Equal(["fresh"], items.Select(i => i.Post.RowKey));
    }

    [Fact]
    public void Ignores_posts_that_are_not_scored()
    {
        var posts = new[]
        {
            Post("new", status: PostStatus.New, score: null),
            Post("sent", status: PostStatus.Sent, score: 9),
            Post("ok", score: 5),
        };

        var items = DigestBuilder.Build(posts, Options(), Now);

        Assert.Equal(["ok"], items.Select(i => i.Post.RowKey));
    }


    [Fact]
    public void Post_published_just_before_the_previous_digest_is_not_missed()
    {
        var posts = new[] { Post("fresh", score: 2, hoursAgo: 2), Post("late", score: 9, hoursAgo: 25) };

        var items = DigestBuilder.Build(posts, Options(), Now);

        Assert.Equal(["late", "fresh"], items.Select(i => i.Post.RowKey));
    }
}
