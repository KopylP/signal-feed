using static PostAgent.Tests.TestData;
using PostAgent.Alerts;
using PostAgent.Posts;

namespace PostAgent.Tests.Alerts;

public class AlertMessageTests
{
    [Fact]
    public void Bundle_shows_message_count_instead_of_author()
    {
        var post = Post("a", type: SourceType.Group, source: "chat", messages: 23);
        post.Author = "ann";

        var html = AlertMessage.Format([post]);

        Assert.Contains("@chat · 23 повідомлень", html);
        Assert.DoesNotContain("ann", html);
    }

    [Fact]
    public void Has_a_header_topic_and_link()
    {
        var html = AlertMessage.Format([Post("a", type: SourceType.Group)]);

        Assert.StartsWith("🚨", html);
        Assert.Contains("<b>Тема</b>", html);
        Assert.Contains("<a href=\"https://t.me/src/1\">Читати</a>", html);
    }
}
