using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Alerts;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Alerts;

public class AlertDeciderTests
{
    private readonly FakeAlertQueue _queue = new();
    private readonly FakeAlertDecisionLock _lock = new();
    private readonly InMemoryFollowedTopicStore _topics = new();

    private static AgentOptions Options(Action<AgentOptions>? configure = null)
    {
        var options = new AgentOptions { AlertSources = "chat", AlertTimeZone = "UTC", Watches = "google core update" };
        configure?.Invoke(options);
        return options;
    }

    private AlertDecider Decider(InMemoryPostRepository repo, Action<AgentOptions>? configure = null) =>
        new(repo, _topics, _lock, _queue, new FakeEffectiveOptions(Options(configure)), new FakeTimeProvider(Now), NullLogger<AlertDecider>.Instance);

    private static PostEntity HotChatPost(string key = "chat_1") => Post(key, score: 9, type: SourceType.Group, source: "chat");

    private static PostEntity Watched(string key = "w1", int score = 2, double hoursAgo = 1, string text = "Google core update is out")
    {
        var post = Post(key, score: score, hoursAgo: hoursAgo, source: "seo_news");
        post.Text = text;
        return post;
    }

    private static byte[] Vector(params float[] head) => PostVectors.ToBytes((float[])[.. head, .. new float[512 - head.Length]]);

    [Fact]
    public async Task Publishes_an_important_group_post_after_marking_it_so_the_cooldown_starts()
    {
        var hot = HotChatPost();
        var repo = new InMemoryPostRepository(hot);

        await Decider(repo).DecideAsync([hot], CancellationToken.None);

        var request = Assert.Single(_queue.Enqueued);
        Assert.Equal(AlertKind.Group, request.Kind);
        Assert.Equal("chat_1", Assert.Single(request.Posts).RowKey);
        Assert.Equal(Now, Assert.Single(repo.AlertsDecided).AlertedAt);
    }

    [Fact]
    public async Task Publishes_a_post_of_any_source_that_matches_a_followed_topic_whatever_its_score()
    {
        var post = Watched(score: 1);
        var repo = new InMemoryPostRepository(post);

        await Decider(repo).DecideAsync([post], CancellationToken.None);

        var request = Assert.Single(_queue.Enqueued);
        Assert.Equal(AlertKind.Topic, request.Kind);
        Assert.Equal("google core update", Assert.Single(request.Posts).Topic);
        Assert.Equal(Now, post.WatchAlertedAt);
        Assert.Null(post.AlertedAt);
    }

    [Fact]
    public async Task Matches_a_topic_by_meaning_with_its_stored_vector_when_no_word_matches()
    {
        var post = Watched(text: "Google оновив основний алгоритм ранжування");
        post.Embedding = Vector(1, 0);
        _topics.Topics["google core update"] = [0.9f, 0.3f, .. new float[510]];
        var repo = new InMemoryPostRepository(post);

        await Decider(repo).DecideAsync([post], CancellationToken.None);

        Assert.Equal(AlertKind.Topic, Assert.Single(_queue.Enqueued).Kind);
    }

    [Fact]
    public async Task Publishes_nothing_for_a_post_whose_meaning_is_below_the_threshold()
    {
        var post = Watched(text: "Bing додав відповіді ШІ");
        post.Embedding = Vector(1, 0);
        _topics.Topics["google core update"] = [0.3f, 0.95f, .. new float[510]];

        await Decider(new InMemoryPostRepository(post)).DecideAsync([post], CancellationToken.None);

        Assert.Empty(_queue.Enqueued);
    }

    [Fact]
    public async Task Still_matches_by_words_when_the_topic_vectors_cannot_be_read()
    {
        var post = Watched();
        _topics.ReadFailure = new InvalidOperationException("storage is down");

        await Decider(new InMemoryPostRepository(post)).DecideAsync([post], CancellationToken.None);

        Assert.Single(_queue.Enqueued);
    }

    [Fact]
    public async Task Publishes_nothing_during_quiet_hours_so_the_posts_stay_for_the_digest()
    {
        var hot = HotChatPost();
        var repo = new InMemoryPostRepository(hot);

        await Decider(repo, o => o.AlertQuietHours = "07:00-09:00").DecideAsync([hot], CancellationToken.None);

        Assert.Empty(_queue.Enqueued);
        Assert.Empty(repo.AlertsDecided);
        Assert.Equal(0, _lock.Acquired);
    }

    [Fact]
    public async Task Never_decides_the_same_post_twice()
    {
        var hot = HotChatPost();
        var watched = Watched();
        var repo = new InMemoryPostRepository(hot, watched);

        await Decider(repo).DecideAsync([hot, watched], CancellationToken.None);
        await Decider(repo).DecideAsync([hot, watched], CancellationToken.None);

        Assert.Equal(2, _queue.Enqueued.Count);
        Assert.Equal((2, 2), (_lock.Acquired, _lock.Released));
    }

    [Fact]
    public async Task Gives_a_group_in_cooldown_no_second_alert_from_another_batch()
    {
        var first = HotChatPost("chat_1");
        var second = HotChatPost("chat_2");
        var repo = new InMemoryPostRepository(first, second);

        await Decider(repo).DecideAsync([first], CancellationToken.None);
        await Decider(repo).DecideAsync([second], CancellationToken.None);

        Assert.Single(_queue.Enqueued);
    }

    [Fact]
    public async Task A_group_post_published_before_the_cooldown_but_alerted_within_it_keeps_the_cooldown()
    {
        var earlier = Post("chat_0", score: 9, hoursAgo: 3.5, status: PostStatus.Sent, type: SourceType.Group, source: "chat", alertedAt: Now.AddHours(-1));
        var hot = HotChatPost("chat_1");
        var repo = new InMemoryPostRepository(earlier, hot);

        await Decider(repo).DecideAsync([hot], CancellationToken.None);

        Assert.Empty(_queue.Enqueued);
    }

    [Fact]
    public async Task A_post_taken_by_a_group_alert_is_not_also_announced_for_its_topic()
    {
        var hot = HotChatPost();
        hot.Text = "Google core update is out";
        var repo = new InMemoryPostRepository(hot);

        await Decider(repo).DecideAsync([hot], CancellationToken.None);

        Assert.Equal(AlertKind.Group, Assert.Single(_queue.Enqueued).Kind);
    }

    [Fact]
    public async Task A_new_topic_announces_its_matches_of_the_last_day_that_nobody_announced()
    {
        var recent = Watched("recent", hoursAgo: 20);
        var old = Watched("old", hoursAgo: 30);
        var sent = Watched("sent");
        sent.Status = PostStatus.Sent;
        var duplicate = Watched("dup");
        duplicate.DuplicateOf = "recent";
        var repo = new InMemoryPostRepository(recent, old, sent, duplicate);

        await Decider(repo, o => o.Watches = "").DecideTopicAsync("google core update", new float[512], CancellationToken.None);

        var request = Assert.Single(_queue.Enqueued);
        Assert.Equal(["recent"], request.Posts.Select(p => p.RowKey));
    }
}
