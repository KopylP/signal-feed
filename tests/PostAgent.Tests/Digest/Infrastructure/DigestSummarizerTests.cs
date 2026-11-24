using System.Text.Json;
using PostAgent.Digest;
using PostAgent.Digest.Infrastructure;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Digest.Infrastructure;

public class DigestSummarizerTests
{
    private const string OneHeadline = """
        {"overview":"Головне","headlines":[{"id":"src_1","title":"Заголовок","summary":"Суть","why_it_matters":"Чому"}],
         "for_marketplace":[{"id":"src_2","title":"Для нас","note":"Нотатка"}],"notable":[],
         "recommended":[{"id":"src_1","why":"Варто прочитати"}],"needs_verification":["Перевірити"]}
        """;

    [Fact]
    public async Task Returns_the_sections_tied_to_our_posts_and_keeps_our_links()
    {
        var items = new[] { new DigestItem(Post("src_1"), IsInteresting: true), new DigestItem(Post("src_2"), IsInteresting: true) };

        var report = await new DigestSummarizer(new FakeChatClient(OneHeadline)).SummarizeAsync(items, CancellationToken.None);

        Assert.Equal("Головне", report.Overview);
        Assert.Equal("Суть", Assert.Single(report.Headlines).Text);
        Assert.Equal("Чому", report.Headlines[0].Why);
        Assert.Equal("Нотатка", Assert.Single(report.ForMarketplace).Text);
        Assert.Equal("Варто прочитати", Assert.Single(report.Recommended).Text);
        Assert.Equal(["Перевірити"], report.NeedsVerification);
        Assert.Equal(items[0].Post.Url, report.Headlines[0].Post.Url);
    }

    [Fact]
    public async Task Sends_post_data_with_the_date_but_without_links_and_trims_long_text()
    {
        var post = Post("src_1", source: "badseo", hoursAgo: 2);
        post.Text = new string('я', 5000);
        var chat = new FakeChatClient("""{"overview":"О","headlines":[],"for_marketplace":[],"notable":[],"recommended":[],"needs_verification":[]}""");

        await new DigestSummarizer(chat).SummarizeAsync([new DigestItem(post, true)], CancellationToken.None);

        using var json = JsonDocument.Parse(chat.Messages[1].Text);
        var sent = json.RootElement[0];
        Assert.Equal("src_1", sent.GetProperty("id").GetString());
        Assert.Equal("badseo", sent.GetProperty("source").GetString());
        Assert.Equal("2026-11-24", sent.GetProperty("published").GetString());
        Assert.True(sent.GetProperty("interesting").GetBoolean());
        Assert.Equal(1500, sent.GetProperty("text").GetString()!.Length);
        Assert.False(sent.TryGetProperty("url", out _));
        Assert.Equal(true, chat.Options!.AdditionalProperties!["strict"]);
    }
}
