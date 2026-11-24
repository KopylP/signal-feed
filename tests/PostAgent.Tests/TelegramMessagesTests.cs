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

        var html = TelegramMessages.FormatDigest([new DigestItem(post, IsInteresting: true)]);

        Assert.Contains("A &lt;b&gt;&amp;&lt;/b&gt; B", html);
        Assert.Contains("x &lt; y", html);
        Assert.Contains("href=\"https://t.me/s/1?a=&quot;q&quot;&amp;b=2\"", html);
        Assert.DoesNotContain("<b>&</b>", html);
    }

    [Fact]
    public void Digest_labels_interesting_and_best_of_the_day_posts()
    {
        var html = TelegramMessages.FormatDigest([
            new DigestItem(Post("a", score: 9), IsInteresting: true),
            new DigestItem(Post("b", score: 3), IsInteresting: false),
        ]);

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
}
