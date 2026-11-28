using Microsoft.Extensions.Logging.Abstractions;
using PostAgent.Alerts;
using PostAgent.Shared.Posts;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Alerts;

public class AlertSenderTests
{
    private static AlertRequest Request(AlertKind kind, params (PostEntity Post, string? Topic)[] items) =>
        new(kind, items.Select(i => new AlertedPost(i.Post.PartitionKey, i.Post.RowKey, i.Topic)).ToList());

    [Fact]
    public async Task Sends_a_group_alert_and_marks_its_posts_sent_without_touching_the_cooldown_mark()
    {
        var post = Post("chat_1", score: 9, type: SourceType.Group, source: "chat");
        var repo = new InMemoryPostRepository(post);
        var notifier = new RecordingNotifier();

        await new AlertSender(repo, notifier, NullLogger<AlertSender>.Instance).SendAsync(Request(AlertKind.Group, (post, null)), CancellationToken.None);

        Assert.Contains("Щось відбувається", Assert.Single(notifier.Sent));
        var mark = Assert.Single(repo.SentMarks);
        Assert.Null(mark.AlertedAt);
        Assert.Equal(["chat_1"], mark.Posts.Select(p => p.RowKey));
    }

    [Fact]
    public async Task Sends_a_topic_alert_with_the_topic_each_post_matched()
    {
        var post = Post("w1");
        var notifier = new RecordingNotifier();

        await new AlertSender(new InMemoryPostRepository(post), notifier, NullLogger<AlertSender>.Instance)
            .SendAsync(Request(AlertKind.Topic, (post, "google core update")), CancellationToken.None);

        Assert.Contains("Тема: «google core update»", Assert.Single(notifier.Sent));
    }

    [Fact]
    public async Task A_failed_send_marks_nothing_so_the_queue_retries_it()
    {
        var post = Post("w1");
        var repo = new InMemoryPostRepository(post);
        var failing = new RecordingNotifier { FailBroadcasts = true };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AlertSender(repo, failing, NullLogger<AlertSender>.Instance).SendAsync(Request(AlertKind.Topic, (post, "x")), CancellationToken.None));

        Assert.Empty(repo.SentMarks);
    }

    [Fact]
    public async Task A_delivered_alert_whose_posts_cannot_be_marked_is_not_failed_so_it_is_not_sent_again()
    {
        var post = Post("w1");
        var notifier = new RecordingNotifier();

        await new AlertSender(new MarkFailingRepository(post), notifier, NullLogger<AlertSender>.Instance)
            .SendAsync(Request(AlertKind.Topic, (post, "x")), CancellationToken.None);

        Assert.Single(notifier.Sent);
    }

    [Fact]
    public async Task Sends_nothing_when_the_posts_are_gone()
    {
        var notifier = new RecordingNotifier();

        await new AlertSender(new InMemoryPostRepository(), notifier, NullLogger<AlertSender>.Instance)
            .SendAsync(Request(AlertKind.Group, (Post("gone"), null)), CancellationToken.None);

        Assert.Empty(notifier.Sent);
    }

    [Fact]
    public void The_request_survives_the_queue_message()
    {
        var request = Request(AlertKind.Topic, (Post("w1"), "ai"));

        var parsed = AlertRequest.Parse(request.ToMessage());

        Assert.Equal(AlertKind.Topic, parsed.Kind);
        Assert.Equal(request.Posts, parsed.Posts);
    }

    [Fact]
    public void The_topic_message_escapes_the_topic_and_the_post()
    {
        var post = Post("w1");
        post.Topic = "<b>evil</b>";

        var html = WatchAlertMessage.Format([new WatchHit(post, "<i>x</i>")]);

        Assert.DoesNotContain("<i>x</i>", html);
        Assert.DoesNotContain("<b>evil</b>", html);
        Assert.Contains("&lt;i&gt;x&lt;/i&gt;", html);
    }

    private sealed class MarkFailingRepository(params PostEntity[] posts) : IPostRepository
    {
        private readonly InMemoryPostRepository _inner = new(posts);

        public Task MarkSentAsync(IEnumerable<PostEntity> posts, DateTimeOffset? alertedAt, CancellationToken ct) => throw new InvalidOperationException("storage is down");

        public Task<IReadOnlyList<PostEntity>> GetAsync(IEnumerable<PostRef> refs, CancellationToken ct) => _inner.GetAsync(refs, ct);

        public Task AddIfMissingAsync(PostEntity post, CancellationToken ct) => _inner.AddIfMissingAsync(post, ct);

        public Task<IReadOnlyList<PostEntity>> GetSinceAsync(DateTimeOffset since, DateTimeOffset now, CancellationToken ct) => _inner.GetSinceAsync(since, now, ct);

        public Task SaveEmbeddingsAsync(IEnumerable<PostEntity> posts, CancellationToken ct) => _inner.SaveEmbeddingsAsync(posts, ct);

        public Task SaveScoresAsync(IEnumerable<PostEntity> posts, CancellationToken ct) => _inner.SaveScoresAsync(posts, ct);

        public Task MarkAlertDecidedAsync(IEnumerable<PostEntity> posts, CancellationToken ct) => _inner.MarkAlertDecidedAsync(posts, ct);

        public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct) => _inner.DeleteOlderThanAsync(cutoff, ct);
    }
}
