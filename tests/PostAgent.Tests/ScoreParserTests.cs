using System.Text.Json;
using PostAgent.Models;
using PostAgent.Services;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests;

public class ScoreParserTests
{
    private const string TwoPosts = """
        {"posts":[
          {"id":"src_1","score":9,"topic":"AI","reason":"Нові цифри","duplicate_of":null},
          {"id":"src_2","score":3,"topic":"AI","reason":"Те саме","duplicate_of":"src_1"}
        ]}
        """;

    [Fact]
    public void Parses_all_fields_including_null_duplicate()
    {
        var scores = ScoreParser.Parse(TwoPosts);

        Assert.Equal(2, scores.Count);
        Assert.Equal(new PostScore("src_1", 9, "AI", "Нові цифри", null), scores[0]);
        Assert.Equal("src_1", scores[1].DuplicateOf);
    }

    [Fact]
    public void Apply_fills_posts_and_marks_them_scored()
    {
        var posts = new[] { Unscored("src_1"), Unscored("src_2") };

        var missing = ScoreParser.Apply(posts, ScoreParser.Parse(TwoPosts));

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

        var missing = ScoreParser.Apply(posts, ScoreParser.Parse(TwoPosts));

        Assert.Equal(["src_3"], missing);
        Assert.Equal(PostStatus.New, posts[1].Status);
        Assert.Null(posts[1].Score);
    }

    [Fact]
    public void Unknown_ids_in_the_response_are_ignored()
    {
        var posts = new[] { Unscored("src_1") };

        var missing = ScoreParser.Apply(posts, ScoreParser.Parse(TwoPosts));

        Assert.Empty(missing);
        Assert.Equal(PostStatus.Scored, posts[0].Status);
    }

    [Fact]
    public void Score_is_clamped_and_self_duplicate_is_dropped()
    {
        var post = Unscored("src_1");
        var scores = new[] { new PostScore("src_1", 42, "T", "R", "src_1") };

        ScoreParser.Apply([post], scores);

        Assert.Equal(10, post.Score);
        Assert.Null(post.DuplicateOf);
    }

    [Fact]
    public void Malformed_response_throws_so_the_queue_retries()
    {
        Assert.ThrowsAny<JsonException>(() => ScoreParser.Parse("not json"));
        Assert.ThrowsAny<JsonException>(() => ScoreParser.Parse("{}"));
    }

    [Fact]
    public void Duplicate_of_an_id_outside_the_batch_is_dropped()
    {
        var post = Unscored("src_2");

        ScoreParser.Apply([post], ScoreParser.Parse(TwoPosts)); // у відповіді src_2 дублює src_1, якого в пачці немає

        Assert.Null(post.DuplicateOf);
    }
}
