using System.Text.Json;
using static PostAgent.Tests.TestData;
using PostAgent.Posts;
using PostAgent.Scoring;
using PostAgent.Tests.Fakes;

namespace PostAgent.Tests.Scoring;

public class ScorerFunctionTests
{
    private static string Message(params PostEntity[] posts) => JsonSerializer.Serialize(posts.Select(p => p.ToRef()));

    [Fact]
    public async Task Scores_the_unscored_posts_and_saves_them()
    {
        var a = Unscored("a");
        var b = Unscored("b");
        var repo = new InMemoryPostRepository(a, b);

        await new ScorerFunction(repo, new FakePostScorer()).Run(Message(a, b), CancellationToken.None);

        Assert.Equal(["a", "b"], repo.ScoresSaved.Select(p => p.RowKey));
        Assert.All(repo.ScoresSaved, p => Assert.Equal(PostStatus.Scored, p.Status));
    }

    [Fact]
    public async Task Skips_posts_that_are_already_scored_so_a_repeated_message_is_harmless()
    {
        var scored = Post("a", score: 8);
        var repo = new InMemoryPostRepository(scored);
        var scorer = new FakePostScorer();

        await new ScorerFunction(repo, scorer).Run(Message(scored), CancellationToken.None);

        Assert.Equal(0, scorer.Calls);
        Assert.Empty(repo.ScoresSaved);
    }

    [Fact]
    public async Task Saves_the_scored_posts_and_then_fails_so_only_the_rest_is_retried()
    {
        var a = Unscored("a");
        var b = Unscored("b");
        var repo = new InMemoryPostRepository(a, b);
        var function = new ScorerFunction(repo, new FakePostScorer(skippedIds: "b"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => function.Run(Message(a, b), CancellationToken.None));

        Assert.Contains("b", error.Message);
        Assert.Equal(["a"], repo.ScoresSaved.Select(p => p.RowKey));
    }
}
