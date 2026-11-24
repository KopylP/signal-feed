using System.Text.Json;
using PostAgent.Digest;
using PostAgent.Digest.Infrastructure;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Digest.Infrastructure;

public class DigestSummarizerTests
{
    [Fact]
    public async Task Returns_the_overview_and_insights_and_keeps_our_links()
    {
        var items = new[] { new DigestItem(Post("src_1"), IsInteresting: true) };
        var chat = new FakeChatClient("""{"overview":"Головне","items":[{"id":"src_1","insight":"Факт"}]}""");

        var report = await new DigestSummarizer(chat).SummarizeAsync(items, CancellationToken.None);

        Assert.Equal("Головне", report.Overview);
        Assert.Equal("Факт", report.Items[0].Insight);
        Assert.Equal(items[0].Post.Url, report.Items[0].Post.Url);
    }

    [Fact]
    public async Task Sends_post_data_without_links_and_trims_long_text()
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
