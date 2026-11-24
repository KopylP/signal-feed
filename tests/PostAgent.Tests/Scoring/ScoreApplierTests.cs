using PostAgent.Scoring;
using PostAgent.Shared.Posts;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Scoring;

public class ScoreApplierTests
{
    private static readonly PostScore[] TwoScores =
    [
        new("src_1", 9, "AI", "Нові цифри", null),
        new("src_2", 3, "AI", "Те саме", "src_1"),
    ];

    [Fact]
    public void Apply_fills_posts_and_marks_them_scored()
    {
        var posts = new[] { Unscored("src_1"), Unscored("src_2") };

        var missing = ScoreApplier.Apply(posts, TwoScores);

        Assert.Empty(missing);
        Assert.Equal(9, posts[0].Score);
        Assert.Equal("Нові цифри", posts[0].Reason);
        Assert.Equal("src_1", posts[1].DuplicateOf);
        Assert.All(posts, p => Assert.Equal(PostStatus.Scored, p.Status));
    }

    [Fact]
    public void Missing_ids_are_reported_and_left_untouched()
    {
        var posts = new[] { Unscored("src_1"), Unscored("src_3") };

        var missing = ScoreApplier.Apply(posts, TwoScores);

        Assert.Equal(["src_3"], missing);
        Assert.Equal(PostStatus.New, posts[1].Status);
        Assert.Null(posts[1].Score);
    }

    [Fact]
    public void Unknown_ids_in_the_response_are_ignored()
    {
        var posts = new[] { Unscored("src_1") };

        var missing = ScoreApplier.Apply(posts, TwoScores);

        Assert.Empty(missing);
        Assert.Equal(PostStatus.Scored, posts[0].Status);
    }

    [Fact]
    public void Score_is_clamped_and_self_duplicate_is_dropped()
    {
        var post = Unscored("src_1");

        ScoreApplier.Apply([post], [new PostScore("src_1", 42, "T", "R", "src_1")]);

        Assert.Equal(10, post.Score);
        Assert.Null(post.DuplicateOf);
    }

    [Fact]
    public void Duplicate_of_an_id_outside_the_batch_is_dropped()
    {
        var post = Unscored("src_2");

        ScoreApplier.Apply([post], TwoScores); // src_2 дублює src_1, якого в пачці немає

        Assert.Null(post.DuplicateOf);
    }
}
