using PostAgent.Models;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests;

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

    [Fact]
    public void MarkSent_keeps_an_earlier_AlertedAt_when_no_alert_time_is_given()
    {
        var post = Post("a", alertedAt: Now.AddHours(-1));

        post.MarkSent();

        Assert.Equal(PostStatus.Sent, post.Status);
        Assert.Equal(Now.AddHours(-1), post.AlertedAt);
    }

    [Fact]
    public void ApplyScore_clamps_the_score_and_drops_a_self_reference()
    {
        var post = Unscored("src_1");

        post.ApplyScore(new PostScore("src_1", 42, "T", "R", "src_1"));

        Assert.Equal(10, post.Score);
        Assert.Null(post.DuplicateOf);
        Assert.Equal(PostStatus.Scored, post.Status);
    }
}
