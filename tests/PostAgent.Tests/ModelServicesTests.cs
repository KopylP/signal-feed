using System.Text.Json;
using Microsoft.Extensions.AI;
using PostAgent.Models;
using PostAgent.Services;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests;

public class ModelServicesTests
{
    private const string TwoScores = """
        {"posts":[
          {"id":"src_1","score":9,"topic":"AI","reason":"Нові цифри","duplicate_of":null},
          {"id":"src_2","score":3,"topic":"AI","reason":"Те саме","duplicate_of":"src_1"}]}
        """;

    [Fact]
    public async Task Scorer_applies_the_scores_from_the_model_response()
    {
        var posts = new[] { Unscored("src_1"), Unscored("src_2") };

        var missing = await new PostScorer(new FakeChatClient(TwoScores)).ScoreAsync(posts, CancellationToken.None);

        Assert.Empty(missing);
        Assert.Equal([9, 3], posts.Select(p => p.Score));
        Assert.Equal("src_1", posts[1].DuplicateOf);
    }

    [Fact]
    public async Task Scorer_reports_posts_the_model_skipped()
    {
        var posts = new[] { Unscored("src_1"), Unscored("src_9") };

        var missing = await new PostScorer(new FakeChatClient(TwoScores)).ScoreAsync(posts, CancellationToken.None);

        Assert.Equal(["src_9"], missing);
    }

    [Fact]
    public async Task Scorer_sends_the_system_prompt_first_and_asks_for_strict_structured_output()
    {
        var chat = new FakeChatClient(TwoScores);
        var post = Unscored("src_1");
        post.Text = "Текст посту";

        await new PostScorer(chat).ScoreAsync([post], CancellationToken.None);

        Assert.Equal([ChatRole.System, ChatRole.User], chat.Messages.Select(m => m.Role));
        Assert.Contains("Шкала", chat.Messages[0].Text);
        Assert.Contains("src_1", chat.Messages[1].Text);
        Assert.Contains("Текст посту", chat.Messages[1].Text); // кирилиця без \uXXXX
        Assert.Equal(true, chat.Options!.AdditionalProperties!["strict"]);
        Assert.NotNull(chat.Options.ResponseFormat);
    }

    [Fact]
    public async Task Scorer_fails_loudly_on_a_malformed_response_so_the_queue_retries()
    {
        var scorer = new PostScorer(new FakeChatClient("not json"));

        await Assert.ThrowsAnyAsync<Exception>(() => scorer.ScoreAsync([Unscored("src_1")], CancellationToken.None));
    }

    [Fact]
    public async Task Summarizer_returns_the_overview_and_insights_and_keeps_our_links()
    {
        var items = new[] { new DigestItem(Post("src_1"), IsInteresting: true) };
        var chat = new FakeChatClient("""{"overview":"Головне","items":[{"id":"src_1","insight":"Факт"}]}""");

        var report = await new DigestSummarizer(chat).SummarizeAsync(items, CancellationToken.None);

        Assert.Equal("Головне", report.Overview);
        Assert.Equal("Факт", report.Items[0].Insight);
        Assert.Equal(items[0].Post.Url, report.Items[0].Post.Url);
    }

    [Fact]
    public async Task Summarizer_sends_post_data_without_links_and_trims_long_text()
    {
        var post = Post("src_1");
        post.Text = new string('я', 5000);
        var chat = new FakeChatClient("""{"overview":"О","items":[]}""");

        await new DigestSummarizer(chat).SummarizeAsync([new DigestItem(post, true)], CancellationToken.None);

        using var json = JsonDocument.Parse(chat.Messages[1].Text);
        var sent = json.RootElement[0];
        Assert.Equal("src_1", sent.GetProperty("id").GetString());
        Assert.Equal(1000, sent.GetProperty("text").GetString()!.Length);
        Assert.False(sent.TryGetProperty("url", out _));
        Assert.Equal(true, chat.Options!.AdditionalProperties!["strict"]);
    }
}
