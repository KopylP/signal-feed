using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PostAgent.Scoring;
using PostAgent.Shared.Posts;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Scoring;

public class ScoringStageTests
{
    private readonly FakeAlertDecider _alerts = new();

    private ScoringStage Stage(InMemoryPostRepository repo, FakePostScorer? scorer = null) =>
        new(repo, scorer ?? new FakePostScorer(), _alerts, NullLogger<ScoringStage>.Instance);

    [Fact]
    public async Task Scores_the_unscored_posts_saves_them_and_decides_their_alerts()
    {
        var a = Unscored("a");
        var b = Unscored("b");
        var repo = new InMemoryPostRepository(a, b);

        await Stage(repo).ScoreAsync([a.ToRef(), b.ToRef()], CancellationToken.None);

        Assert.Equal(["a", "b"], repo.ScoresSaved.Select(p => p.RowKey));
        Assert.All(repo.ScoresSaved, p => Assert.Equal(PostStatus.Scored, p.Status));
        Assert.Equal(["a", "b"], Assert.Single(_alerts.Decisions).Select(p => p.RowKey));
    }

    [Fact]
    public async Task Skips_posts_that_are_already_scored_so_a_repeated_message_is_harmless()
    {
        var scored = Post("a", score: 8);
        var repo = new InMemoryPostRepository(scored);
        var scorer = new FakePostScorer();

        await Stage(repo, scorer).ScoreAsync([scored.ToRef()], CancellationToken.None);

        Assert.Equal(0, scorer.Calls);
        Assert.Empty(repo.ScoresSaved);
        Assert.Empty(_alerts.Decisions);
    }

    [Fact]
    public async Task Saves_the_scored_posts_and_then_fails_so_only_the_rest_is_retried()
    {
        var a = Unscored("a");
        var b = Unscored("b");
        var repo = new InMemoryPostRepository(a, b);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Stage(repo, new FakePostScorer(skippedIds: "b")).ScoreAsync([a.ToRef(), b.ToRef()], CancellationToken.None));

        Assert.Contains("b", error.Message);
        Assert.Equal(["a"], repo.ScoresSaved.Select(p => p.RowKey));
    }

    [Fact]
    public async Task A_failed_alert_decision_does_not_fail_the_scoring()
    {
        var a = Unscored("a");
        var repo = new InMemoryPostRepository(a);
        _alerts.Failure = new TimeoutException("lock busy");

        await Stage(repo).ScoreAsync([a.ToRef()], CancellationToken.None);

        Assert.Single(repo.ScoresSaved);
    }

    [Fact]
    public async Task The_function_reads_the_references_of_the_queue_message()
    {
        var a = Unscored("a");
        var repo = new InMemoryPostRepository(a);

        await new ScorerFunction(Stage(repo)).Run(JsonSerializer.Serialize(new[] { a.ToRef() }), CancellationToken.None);

        Assert.Single(repo.ScoresSaved);
    }
}
