using PostAgent.Shared.Chat;
using PostAgent.Shared.Feedback;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using PostAgent.Tests.SettingsMenu;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Chat.Agent.Tools;

public class RatePostToolTests
{
    private static readonly PostAgent.Shared.Posts.PostEntity Stored = Post("chan_7", source: "chan");
    private static readonly string Id = ChatPostId.Format(Stored.ToRef());

    private static (ToolShim Tools, MenuHarness Harness) Create(Action<AgentOptions>? configure = null)
    {
        var harness = new MenuHarness(configure) { Posts = new InMemoryPostRepository(Stored) };
        return (new ToolShim(harness.Tools([], ChatId)), harness);
    }

    [Fact]
    public async Task A_like_is_saved_at_once_with_the_source_and_topic_of_the_post()
    {
        var (tools, harness) = Create();

        var answer = await tools.RatePost(Id, liked: true, CancellationToken.None);

        Assert.StartsWith("Done", answer);
        var item = Assert.Single(harness.Feedback.Items);
        Assert.Equal((Id, "chan", "Тема", FeedbackVote.Up, Now), (item.PostId, item.Source, item.Topic, item.Vote, item.At));
        Assert.Null(harness.Store.Current); // no plan and no proposal: nothing waits for a tap
    }

    [Fact]
    public async Task A_dislike_replaces_an_earlier_like_of_the_same_post()
    {
        var (tools, harness) = Create();

        await tools.RatePost(Id, liked: true, CancellationToken.None);
        await tools.RatePost(Id, liked: false, CancellationToken.None);

        Assert.Equal(FeedbackVote.Down, Assert.Single(harness.Feedback.Items).Vote);
    }

    [Theory]
    [InlineData("2026-11-24/missing_1")]
    [InlineData("not an id")]
    public async Task An_unknown_post_is_not_saved_and_the_agent_is_told_to_ask(string id)
    {
        var (tools, harness) = Create();

        var answer = await tools.RatePost(id, liked: true, CancellationToken.None);

        Assert.Contains("Post not found", answer);
        Assert.Empty(harness.Feedback.Items);
    }

    [Fact]
    public async Task Nothing_is_saved_while_feedback_is_switched_off()
    {
        var (tools, harness) = Create(o => o.FeedbackEnabled = false);

        var answer = await tools.RatePost(Id, liked: true, CancellationToken.None);

        Assert.Contains("switched off", answer);
        Assert.Empty(harness.Feedback.Items);
    }

    [Fact]
    public async Task A_storage_failure_is_told_to_the_agent_not_thrown()
    {
        var (tools, harness) = Create();
        harness.Feedback.ThrowOnRecord = true;

        var answer = await tools.RatePost(Id, liked: true, CancellationToken.None);

        Assert.StartsWith("Not saved", answer);
    }
}
