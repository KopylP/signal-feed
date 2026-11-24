using PostAgent.Notifications;

namespace PostAgent.Tests.Notifications;

public class TelegramHtmlTests
{
    [Theory]
    [InlineData("a < b && c > d", "a &lt; b &amp;&amp; c &gt; d")]
    [InlineData("<script>alert(1)</script>", "&lt;script&gt;alert(1)&lt;/script&gt;")]
    [InlineData("&lt;", "&amp;lt;")]
    [InlineData("звичайний текст", "звичайний текст")]
    [InlineData(null, "")]
    public void Escape_handles_html_special_characters(string? input, string expected)
    {
        Assert.Equal(expected, TelegramHtml.Escape(input));
    }
}
