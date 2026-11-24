using PostAgent.Models;
using PostAgent.Services;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests;

public class TelegramMessagesTests
{
    [Theory]
    [InlineData("a < b && c > d", "a &lt; b &amp;&amp; c &gt; d")]
    [InlineData("<script>alert(1)</script>", "&lt;script&gt;alert(1)&lt;/script&gt;")]
    [InlineData("&lt;", "&amp;lt;")]
    [InlineData("звичайний текст", "звичайний текст")]
    [InlineData(null, "")]
    public void Escape_handles_html_special_characters(string? input, string expected)
    {
        Assert.Equal(expected, TelegramMessages.Escape(input));
    }

    [Fact]
    public void Digest_escapes_model_generated_text_and_the_url()
    {
        var post = Post("a", score: 9);
        post.Topic = "A <b>&</b> B";
        post.Reason = "x < y";
        post.Url = "https://t.me/s/1?a=\"q\"&b=2";

        var html = TelegramMessages.FormatDigest(DigestReport.Plain([new DigestItem(post, IsInteresting: true)]));

        Assert.Contains("A &lt;b&gt;&amp;&lt;/b&gt; B", html);
        Assert.Contains("x &lt; y", html);
        Assert.Contains("href=\"https://t.me/s/1?a=&quot;q&quot;&amp;b=2\"", html);
        Assert.DoesNotContain("<b>&</b>", html);
    }

    [Fact]
    public void Digest_labels_interesting_and_best_of_the_day_posts()
    {
        var html = TelegramMessages.FormatDigest(DigestReport.Plain([
            new DigestItem(Post("a", score: 9), IsInteresting: true),
            new DigestItem(Post("b", score: 3), IsInteresting: false),
        ]));

        Assert.Contains("🔥 <b>Цікаве</b>", html);
        Assert.Contains("📌 <b>Найкраще за день</b>", html);
    }

    [Fact]
    public void Bundle_shows_message_count_instead_of_author()
    {
        var post = Post("a", type: SourceType.Group, source: "chat", messages: 23);
        post.Author = "ann";

        var html = TelegramMessages.FormatAlert([post]);

        Assert.Contains("@chat · 23 повідомлень", html);
        Assert.DoesNotContain("ann", html);
    }

    [Fact]
    public void Alert_has_a_header_topic_and_link()
    {
        var html = TelegramMessages.FormatAlert([Post("a", type: SourceType.Group)]);

        Assert.StartsWith("🚨", html);
        Assert.Contains("<b>Тема</b>", html);
        Assert.Contains("<a href=\"https://t.me/src/1\">Читати</a>", html);
    }

    [Fact]
    public void Health_message_lists_failures_escaped()
    {
        var html = TelegramMessages.FormatHealth(["@news: <boom> & more", "@chat: timeout"]);

        Assert.StartsWith("⚠️", html);
        Assert.Contains("@news: &lt;boom&gt; &amp; more", html);
        Assert.Contains("@chat: timeout", html);
    }

    [Fact]
    public void Digest_shows_the_overview_and_prefers_the_insight_over_the_reason()
    {
        var report = new DigestReport("Головне: <нові> ціни", [new DigestItem(Post("a"), IsInteresting: true, Insight: "Конкретний факт")]);

        var html = TelegramMessages.FormatDigest(report);

        Assert.Contains("Головне: &lt;нові&gt; ціни", html);
        Assert.Contains("Конкретний факт", html);
        Assert.DoesNotContain("Причина", html);
    }

    [Fact]
    public void Digest_falls_back_to_the_reason_when_there_is_no_insight()
    {
        var html = TelegramMessages.FormatDigest(DigestReport.Plain([new DigestItem(Post("a"), IsInteresting: true)]));

        Assert.Contains("Причина", html);
    }

    [Fact]
    public void Digest_with_the_maximum_items_fits_into_one_telegram_message()
    {
        var longPost = Post("a");
        longPost.Topic = new string('т', 500);
        longPost.Reason = new string('р', 500);
        var items = Enumerable.Range(0, TelegramMessages.MaxItems).Select(_ => new DigestItem(longPost, true, new string('і', 500))).ToList();

        var html = TelegramMessages.FormatDigest(new DigestReport(new string('о', 2000), items));

        Assert.True(html.Length <= 4096, $"Message is {html.Length} chars");
    }
}
