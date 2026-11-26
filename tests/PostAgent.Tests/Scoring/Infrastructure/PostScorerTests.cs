using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using PostAgent.Scoring.Infrastructure;
using PostAgent.Shared.Feedback;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Scoring.Infrastructure;

public class PostScorerTests
{
    private const string TwoScores = """
        {"posts":[
          {"id":"src_1","score":9,"topic":"AI","reason":"Нові цифри","duplicate_of":null},
          {"id":"src_2","score":3,"topic":"AI","reason":"Те саме","duplicate_of":"src_1"}]}
        """;

    private static PostScorer Scorer(IChatClient chat, InMemoryFeedbackStore? feedback = null, Action<AgentOptions>? configure = null)
    {
        var options = new AgentOptions();
        configure?.Invoke(options);
        return new PostScorer(chat, new DefaultPrompts(), feedback ?? new InMemoryFeedbackStore(), new FakeEffectiveOptions(options), NullLogger<PostScorer>.Instance);
    }

    private static InMemoryFeedbackStore WithVotes()
    {
        var store = new InMemoryFeedbackStore();
        store.Seed(new FeedbackItem("d/a", "chan", "Оновлення Google", FeedbackVote.Up, Now));
        return store;
    }

    [Fact]
    public async Task The_readers_votes_go_before_the_posts_in_the_user_message()
    {
        var chat = new FakeChatClient(TwoScores);
        var post = Unscored("src_1");

        await Scorer(chat, WithVotes()).ScoreAsync([post], CancellationToken.None);

        var user = chat.Messages[1].Text;
        Assert.StartsWith("<reader_feedback>\n+ Оновлення Google (@chan)\n</reader_feedback>", user);
        Assert.True(user.IndexOf("src_1", StringComparison.Ordinal) > user.IndexOf("</reader_feedback>", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Without_votes_the_user_message_is_only_the_posts()
    {
        var chat = new FakeChatClient(TwoScores);

        await Scorer(chat).ScoreAsync([Unscored("src_1")], CancellationToken.None);

        Assert.DoesNotContain("reader_feedback", chat.Messages[1].Text);
    }

    [Fact]
    public async Task With_feedback_switched_off_the_votes_are_not_sent()
    {
        var chat = new FakeChatClient(TwoScores);

        await Scorer(chat, WithVotes(), o => o.FeedbackEnabled = false).ScoreAsync([Unscored("src_1")], CancellationToken.None);

        Assert.DoesNotContain("reader_feedback", chat.Messages[1].Text);
    }

    [Fact]
    public async Task A_storage_failure_reading_the_votes_does_not_stop_the_scoring()
    {
        var store = WithVotes();
        store.ThrowOnLoad = true;
        var posts = new[] { Unscored("src_1"), Unscored("src_2") };

        var missing = await Scorer(new FakeChatClient(TwoScores), store).ScoreAsync(posts, CancellationToken.None);

        Assert.Empty(missing);
        Assert.Equal(9, posts[0].Score);
    }

    [Fact]
    public async Task Applies_the_scores_from_the_model_response()
    {
        var posts = new[] { Unscored("src_1"), Unscored("src_2") };

        var missing = await Scorer(new FakeChatClient(TwoScores)).ScoreAsync(posts, CancellationToken.None);

        Assert.Empty(missing);
        Assert.Equal([9, 3], posts.Select(p => p.Score));
        Assert.Equal("src_1", posts[1].DuplicateOf);
    }

    [Fact]
    public async Task Reports_posts_the_model_skipped()
    {
        var posts = new[] { Unscored("src_1"), Unscored("src_9") };

        var missing = await Scorer(new FakeChatClient(TwoScores)).ScoreAsync(posts, CancellationToken.None);

        Assert.Equal(["src_9"], missing);
    }

    [Fact]
    public async Task Sends_the_system_prompt_first_and_asks_for_strict_structured_output()
    {
        var chat = new FakeChatClient(TwoScores);
        var post = Unscored("src_1");
        post.Text = "Текст посту";

        await Scorer(chat).ScoreAsync([post], CancellationToken.None);

        Assert.Equal([ChatRole.System, ChatRole.User], chat.Messages.Select(m => m.Role));
        Assert.Contains("## Scale", chat.Messages[0].Text);
        Assert.Contains("src_1", chat.Messages[1].Text);
        Assert.Contains("Текст посту", chat.Messages[1].Text); // кирилиця без \uXXXX
        Assert.Equal(true, chat.Options!.AdditionalProperties!["strict"]);
        Assert.NotNull(chat.Options.ResponseFormat);
    }

    [Fact]
    public async Task Fails_loudly_on_a_malformed_response_so_the_queue_retries()
    {
        var scorer = Scorer(new FakeChatClient("not json"));

        await Assert.ThrowsAnyAsync<Exception>(() => scorer.ScoreAsync([Unscored("src_1")], CancellationToken.None));
    }
}
