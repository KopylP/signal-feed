using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Alerts;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Alerts;

public class WatchAlertTests
{
    private readonly RecordingNotifier _notifier = new();

    private static AgentOptions Options(Action<AgentOptions>? configure = null)
    {
        var options = new AgentOptions { AlertSources = "chat", AlertTimeZone = "UTC", Watches = "google core update" };
        configure?.Invoke(options);
        return options;
    }

    private AlertJob Job(InMemoryPostRepository repo, Action<AgentOptions>? configure = null) =>
        new(repo, _notifier, new FakeEffectiveOptions(Options(configure)), new FakeTimeProvider(Now), NullLogger<AlertJob>.Instance);

    private static PostEntity Watched(string key = "w1", int? score = 2, double hoursAgo = 1, string source = "seo_news", SourceType type = SourceType.Channel, PostStatus status = PostStatus.Scored, string text = "Google core update is out")
    {
        var post = Post(key, score: score, hoursAgo: hoursAgo, source: source, type: type, status: status);
        post.Text = text;
        post.Topic = "оновлення";
        post.Reason = "Причина";
        return post;
    }

    private static PostEntity Duplicate()
    {
        var post = Watched("dup", score: 5);
        post.DuplicateOf = "w1";
        return post;
    }

    [Fact]
    public async Task A_new_post_that_matches_a_followed_topic_is_sent_whatever_its_score_and_source()
    {
        var repo = new InMemoryPostRepository(Watched(score: 1, source: "seo_news", type: SourceType.Channel));

        await Job(repo).RunAsync(CancellationToken.None);

        var message = Assert.Single(_notifier.Sent);
        Assert.Contains("Стеження за темою", message);
        Assert.Contains("«google core update»", message);
        Assert.Contains("@seo_news", message);
    }

    [Fact]
    public async Task A_watched_post_is_marked_sent_without_an_alert_time_so_it_is_announced_once_and_starts_no_cooldown()
    {
        var repo = new InMemoryPostRepository(Watched());

        await Job(repo).RunAsync(CancellationToken.None);

        var (marked, alertedAt) = Assert.Single(repo.SentMarks);
        Assert.Equal(["w1"], marked.Select(p => p.RowKey));
        Assert.Null(alertedAt);
    }

    [Fact]
    public async Task Posts_that_were_already_sent_or_are_duplicates_or_old_or_unscored_are_not_announced()
    {
        var repo = new InMemoryPostRepository(
            Watched("sent", status: PostStatus.Sent),
            Duplicate(),
            Watched("old", hoursAgo: 30),
            Unscored("new"));

        await Job(repo).RunAsync(CancellationToken.None);

        Assert.Empty(_notifier.Sent);
    }

    [Fact]
    public async Task Nothing_is_sent_without_a_followed_topic_or_when_no_post_matches()
    {
        var repo = new InMemoryPostRepository(Watched());

        await Job(repo, o => o.Watches = string.Empty).RunAsync(CancellationToken.None);
        await Job(repo, o => o.Watches = "ai overviews").RunAsync(CancellationToken.None);

        Assert.Empty(_notifier.Sent);
    }

    [Fact]
    public async Task A_low_scored_match_from_before_the_quiet_hours_ended_is_still_announced_afterwards()
    {
        // 20 hours old: far outside the 6 hour alert window, inside the day the followed topics look at.
        var repo = new InMemoryPostRepository(Watched(score: 2, hoursAgo: 20));

        await Job(repo).RunAsync(CancellationToken.None);

        Assert.Single(_notifier.Sent);
    }

    [Fact]
    public async Task A_short_alert_window_does_not_shorten_the_followed_topics()
    {
        var repo = new InMemoryPostRepository(Watched(hoursAgo: 3));

        await Job(repo, o =>
        {
            o.AlertWindowHours = 1;
            o.AlertCooldownHours = 0;
        }).RunAsync(CancellationToken.None);

        Assert.Single(_notifier.Sent);
    }

    [Fact]
    public async Task Quiet_hours_hold_the_watch_alerts_back_too()
    {
        var repo = new InMemoryPostRepository(Watched());

        await Job(repo, o => o.AlertQuietHours = "00:00-23:59").RunAsync(CancellationToken.None);

        Assert.Empty(_notifier.Sent);
        Assert.Empty(repo.SentMarks);
    }

    [Fact]
    public async Task A_post_the_regular_alert_took_in_the_same_run_is_not_announced_twice()
    {
        var hot = Watched("hot", score: 9, source: "chat", type: SourceType.Group);
        var repo = new InMemoryPostRepository(hot);

        await Job(repo).RunAsync(CancellationToken.None);

        Assert.Single(_notifier.Sent);
        Assert.Contains("Щось відбувається", _notifier.Sent[0]);
    }

    [Fact]
    public async Task A_failed_send_marks_nothing_so_the_next_run_tries_again()
    {
        var failing = new RecordingNotifier { FailBroadcasts = true };
        var repo = new InMemoryPostRepository(Watched());
        var job = new AlertJob(repo, failing, new FakeEffectiveOptions(Options()), new FakeTimeProvider(Now), NullLogger<AlertJob>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => job.RunAsync(CancellationToken.None));

        Assert.Empty(repo.SentMarks);
    }

    [Fact]
    public void One_run_announces_at_most_a_few_posts_best_first()
    {
        var posts = Enumerable.Range(1, 9).Select(i => Watched("w" + i, score: i, source: "s" + i)).ToList();

        var hits = AlertSelector.SelectWatched(posts, Options(), Now, new HashSet<string>());

        Assert.Equal(AlertSelector.MaxWatchedPerRun, hits.Count);
        Assert.Equal(["w9", "w8", "w7", "w6", "w5"], hits.Select(h => h.Post.RowKey));
    }

    [Fact]
    public void The_message_escapes_the_topic_and_the_post()
    {
        var post = Watched();
        post.Topic = "<b>evil</b>";

        var html = WatchAlertMessage.Format([new WatchHit(post, "<i>x</i>")]);

        Assert.DoesNotContain("<i>x</i>", html);
        Assert.DoesNotContain("<b>evil</b>", html);
        Assert.Contains("&lt;i&gt;x&lt;/i&gt;", html);
    }
}
