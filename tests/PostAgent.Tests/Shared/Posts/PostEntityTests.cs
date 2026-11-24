using static PostAgent.Tests.TestData;
using PostAgent.Scoring;
using PostAgent.Shared.Posts;

namespace PostAgent.Tests.Shared.Posts;

public class PostEntityTests
{
    [Fact]
    public void Partition_is_the_utc_date_of_publication()
    {
        var lateEvening = new DateTimeOffset(2026, 11, 24, 1, 30, 0, TimeSpan.FromHours(3)); // 23 листопада за UTC

        Assert.Equal("2026-11-23", PostEntity.PartitionFor(lateEvening));
    }

    [Fact]
    public void One_day_retention_keeps_yesterdays_and_todays_partitions()
    {
        var cutoffPartition = PostEntity.PartitionFor(Now.AddDays(-1));

        Assert.Equal("2026-11-23", cutoffPartition);
        Assert.True(string.CompareOrdinal(PostEntity.PartitionFor(Now.AddDays(-2)), cutoffPartition) < 0); // видаляється
        Assert.False(string.CompareOrdinal(PostEntity.PartitionFor(Now.AddHours(-20)), cutoffPartition) < 0); // лишається
    }

    private static readonly IReadOnlySet<string> Batch = new HashSet<string> { "src_1", "src_2" };

    [Fact]
    public void ApplyScore_clamps_the_score_and_marks_the_post_scored()
    {
        var post = Unscored("src_1");

        post.ApplyScore(new PostScore("src_1", 42, "T", "R", null), Batch);

        Assert.Equal(10, post.Score);
        Assert.Equal(PostStatus.Scored, post.Status);
    }

    [Theory]
    [InlineData("src_2", "src_2")]   // інший пост із пачки
    [InlineData("src_1", null)]      // посилання на себе
    [InlineData("вигаданий", null)]  // id, якого немає в пачці
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ApplyScore_keeps_DuplicateOf_only_when_it_points_to_another_post_of_the_batch(string? duplicateOf, string? expected)
    {
        var post = Unscored("src_1");

        post.ApplyScore(new PostScore("src_1", 5, "T", "R", duplicateOf), Batch);

        Assert.Equal(expected, post.DuplicateOf);
    }
}
