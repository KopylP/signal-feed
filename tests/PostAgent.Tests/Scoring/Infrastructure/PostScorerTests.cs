using Microsoft.Extensions.AI;
using PostAgent.Scoring.Infrastructure;
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

    [Fact]
    public async Task Applies_the_scores_from_the_model_response()
    {
        var posts = new[] { Unscored("src_1"), Unscored("src_2") };

        var missing = await new PostScorer(new FakeChatClient(TwoScores)).ScoreAsync(posts, CancellationToken.None);

        Assert.Empty(missing);
        Assert.Equal([9, 3], posts.Select(p => p.Score));
        Assert.Equal("src_1", posts[1].DuplicateOf);
    }

    [Fact]
    public async Task Reports_posts_the_model_skipped()
    {
        var posts = new[] { Unscored("src_1"), Unscored("src_9") };

        var missing = await new PostScorer(new FakeChatClient(TwoScores)).ScoreAsync(posts, CancellationToken.None);

        Assert.Equal(["src_9"], missing);
    }

    [Fact]
    public async Task Sends_the_system_prompt_first_and_asks_for_strict_structured_output()
    {
        var chat = new FakeChatClient(TwoScores);
        var post = Unscored("src_1");
        post.Text = "Текст посту";

        await new PostScorer(chat).ScoreAsync([post], CancellationToken.None);

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
        var scorer = new PostScorer(new FakeChatClient("not json"));

        await Assert.ThrowsAnyAsync<Exception>(() => scorer.ScoreAsync([Unscored("src_1")], CancellationToken.None));
    }
}
