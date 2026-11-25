using PostAgent.Shared.Chat;
using PostAgent.Shared.Posts;

namespace PostAgent.Tests.Shared.Chat;

public class ChatPostIdTests
{
    [Fact]
    public void Format_joins_the_partition_and_the_row()
    {
        Assert.Equal("2026-11-25/news_42", ChatPostId.Format(new PostRef("2026-11-25", "news_42")));
    }

    [Fact]
    public void A_formatted_id_parses_back_to_the_same_post()
    {
        var post = new PostRef("2026-11-25", "news_42-45");

        Assert.True(ChatPostId.TryParse(ChatPostId.Format(post), out var parsed));
        Assert.Equal(post, parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("a/b/c")]
    [InlineData("/b")]
    [InlineData("a/")]
    public void A_malformed_id_does_not_parse(string id)
    {
        Assert.False(ChatPostId.TryParse(id, out _));
    }
}
