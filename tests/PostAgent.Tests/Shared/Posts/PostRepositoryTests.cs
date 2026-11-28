using Azure;
using Microsoft.Extensions.Logging.Abstractions;
using PostAgent.Shared.Infrastructure;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Posts.Storage;
using PostAgent.Tests.Fakes;

namespace PostAgent.Tests.Shared.Posts;

public sealed class PostRepositoryTests
{
    private readonly FakeTableServiceClient _tables = new();

    private FakeTableClient Table => _tables.Table(StorageNames.PostsTable);

    private PostRepository Repository() => new(_tables, NullLogger<PostRepository>.Instance);

    [Theory]
    [InlineData(412)]
    [InlineData(404)]
    public async Task A_score_for_a_post_changed_or_deleted_meanwhile_is_skipped_and_the_rest_are_saved(int status)
    {
        Table.UpdateFailures["a"] = status;

        await Repository().SaveScoresAsync([TestData.Post("a"), TestData.Post("b")], CancellationToken.None);

        Assert.Equal(["b"], Table.Updates.Select(u => u.RowKey));
    }

    [Fact]
    public async Task Scores_are_written_only_if_the_row_is_unchanged_and_alert_marks_always()
    {
        var post = TestData.Post("a");
        post.ETag = new ETag("v1");
        post.AlertedAt = TestData.Now;

        await Repository().SaveScoresAsync([post], CancellationToken.None);
        await Repository().MarkAlertDecidedAsync([post], CancellationToken.None);

        Assert.Equal([new ETag("v1"), ETag.All], Table.Updates.Select(u => u.IfMatch));
    }

    [Fact]
    public async Task Reads_keep_the_order_across_groups_and_skip_missing_posts()
    {
        var keys = Enumerable.Range(0, 45).Select(i => $"p{i}").ToList();
        foreach (var key in keys.Where((_, i) => i % 3 != 0))
        {
            Table.Add(TestData.Post(key));
        }

        var posts = await Repository().GetAsync(keys.Select(k => new PostRef(TestData.Post(k).PartitionKey, k)), CancellationToken.None);

        Assert.Equal(keys.Where((_, i) => i % 3 != 0), posts.Select(p => p.RowKey));
    }
}
