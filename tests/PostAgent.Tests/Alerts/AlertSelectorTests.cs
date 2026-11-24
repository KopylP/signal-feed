using PostAgent.Alerts;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Alerts;

public class AlertSelectorTests
{
    private static AgentOptions Options(Action<AgentOptions>? configure = null) =>
        TestData.Options(o =>
        {
            o.AlertSources = "chat";
            configure?.Invoke(o);
        });

    private static PostEntity ChatPost(string rowKey, int score = 5, int messages = 1, string source = "chat",
        double hoursAgo = 1, DateTimeOffset? alertedAt = null, PostStatus status = PostStatus.Scored) =>
        Post(rowKey, score, hoursAgo, status, SourceType.Group, source, messages, alertedAt: alertedAt);

    [Fact]
    public void High_score_in_a_selected_group_triggers_an_alert()
    {
        var result = AlertSelector.Select([ChatPost("a", score: 9)], Options(), Now);
        Assert.Equal(["a"], result.Select(p => p.RowKey));
    }

    [Fact]
    public void Score_below_threshold_and_small_bundle_do_not_trigger()
    {
        Assert.Empty(AlertSelector.Select([ChatPost("a", score: 8, messages: 7)], Options(), Now));
    }

    [Fact]
    public void Large_bundle_triggers_even_with_a_low_score()
    {
        var result = AlertSelector.Select([ChatPost("a", score: 2, messages: 8)], Options(), Now);
        Assert.Single(result);
    }

    [Fact]
    public void Channels_never_trigger_alerts()
    {
        var channelPost = Post("a", score: 10, type: SourceType.Channel, source: "chat");
        Assert.Empty(AlertSelector.Select([channelPost], Options(), Now));
    }

    [Fact]
    public void Groups_outside_AlertSources_do_not_trigger()
    {
        Assert.Empty(AlertSelector.Select([ChatPost("a", score: 10, source: "other")], Options(), Now));
    }

    [Fact]
    public void Posts_older_than_the_window_or_already_sent_are_ignored()
    {
        var posts = new[]
        {
            ChatPost("old", score: 10, hoursAgo: 5),
            ChatPost("sent", score: 10, status: PostStatus.Sent),
        };
        Assert.Empty(AlertSelector.Select(posts, Options(o => o.AlertWindowHours = 3), Now));
    }

    [Fact]
    public void Source_in_cooldown_is_skipped()
    {
        var posts = new[]
        {
            ChatPost("earlier", score: 9, status: PostStatus.Sent, alertedAt: Now.AddHours(-1)),
            ChatPost("new", score: 10),
        };

        Assert.Empty(AlertSelector.Select(posts, Options(o => o.AlertCooldownHours = 3), Now));
    }

    [Fact]
    public void Source_is_alerted_again_after_the_cooldown()
    {
        var posts = new[]
        {
            ChatPost("earlier", score: 9, status: PostStatus.Sent, alertedAt: Now.AddHours(-4), hoursAgo: 5),
            ChatPost("new", score: 10),
        };

        var result = AlertSelector.Select(posts, Options(o => o.AlertCooldownHours = 3), Now);
        Assert.Equal(["new"], result.Select(p => p.RowKey));
    }

    [Fact]
    public void Only_the_best_post_per_source_is_alerted()
    {
        var posts = new[] { ChatPost("a", score: 9), ChatPost("b", score: 10) };
        var result = AlertSelector.Select(posts, Options(), Now);
        Assert.Equal(["b"], result.Select(p => p.RowKey));
    }

    [Fact]
    public void Alerts_per_run_are_limited_to_the_best_sources()
    {
        var posts = new[]
        {
            ChatPost("a", score: 9, source: "g1"),
            ChatPost("b", score: 10, source: "g2"),
            ChatPost("c", score: 9, messages: 12, source: "g3"),
            ChatPost("d", score: 9, source: "g4"),
        };

        var result = AlertSelector.Select(posts,
            Options(o =>
            {
                o.AlertSources = "g1,g2,g3,g4";
                o.AlertMaxPerRun = 2;
            }), Now);

        Assert.Equal(["b", "c"], result.Select(p => p.RowKey));
    }
}
