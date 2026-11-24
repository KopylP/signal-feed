using PostAgent.Digest;
using PostAgent.Shared.Settings;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Digest;

public class DigestMessageTests
{
    [Fact]
    public void Escapes_model_generated_text_and_the_url()
    {
        var post = Post("a", score: 9);
        post.Topic = "A <b>&</b> B";
        post.Reason = "x < y";
        post.Url = "https://t.me/s/1?a=\"q\"&b=2";

        var html = DigestMessage.Format(DigestReport.Plain([new DigestItem(post, IsInteresting: true)]));

        Assert.Contains("A &lt;b&gt;&amp;&lt;/b&gt; B", html);
        Assert.Contains("x &lt; y", html);
        Assert.Contains("href=\"https://t.me/s/1?a=&quot;q&quot;&amp;b=2\"", html);
        Assert.DoesNotContain("<b>&</b>", html);
    }

    [Fact]
    public void Labels_interesting_and_best_of_the_day_posts()
    {
        var html = DigestMessage.Format(DigestReport.Plain([
            new DigestItem(Post("a", score: 9), IsInteresting: true),
            new DigestItem(Post("b", score: 3), IsInteresting: false),
        ]));

        Assert.Contains("🔥 <b>Цікаве</b>", html);
        Assert.Contains("📌 <b>Найкраще за день</b>", html);
    }

    [Fact]
    public void Shows_the_overview_and_prefers_the_insight_over_the_reason()
    {
        var report = new DigestReport("Головне: <нові> ціни", [new DigestItem(Post("a"), IsInteresting: true, Insight: "Конкретний факт")]);

        var html = DigestMessage.Format(report);

        Assert.Contains("Головне: &lt;нові&gt; ціни", html);
        Assert.Contains("Конкретний факт", html);
        Assert.DoesNotContain("Причина", html);
    }

    [Fact]
    public void Falls_back_to_the_reason_when_there_is_no_insight()
    {
        var html = DigestMessage.Format(DigestReport.Plain([new DigestItem(Post("a"), IsInteresting: true)]));

        Assert.Contains("Причина", html);
    }

    [Fact]
    public void The_maximum_number_of_items_fits_into_one_telegram_message()
    {
        var longPost = Post("a");
        longPost.Topic = new string('т', 500);
        longPost.Reason = new string('р', 500);
        var items = Enumerable.Range(0, AgentOptions.MaxItemsPerMessage).Select(_ => new DigestItem(longPost, true, new string('і', 500))).ToList();

        var html = DigestMessage.Format(new DigestReport(new string('о', 2000), items));

        Assert.True(html.Length <= 4096, $"Message is {html.Length} chars");
    }
}
