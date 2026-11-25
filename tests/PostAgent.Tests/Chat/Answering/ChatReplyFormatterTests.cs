using System.Net;
using PostAgent.Chat.Answering;

namespace PostAgent.Tests.Chat.Answering;

public class ChatReplyFormatterTests
{
    private const int TelegramLimit = 4096;

    [Fact]
    public void Html_in_the_reply_is_escaped()
    {
        var parts = ChatReplyFormatter.Split("a < b & c");

        Assert.Equal(["a &lt; b &amp; c"], parts);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n ")]
    public void An_empty_reply_gives_no_parts(string text)
    {
        Assert.Empty(ChatReplyFormatter.Split(text));
    }

    [Fact]
    public void A_short_reply_is_one_part()
    {
        Assert.Single(ChatReplyFormatter.Split("Коротка відповідь"));
    }

    [Fact]
    public void A_long_reply_without_any_line_break_is_split_into_parts_that_fit_telegram()
    {
        var text = string.Concat(Enumerable.Repeat("слово", 2000)); // 10 000 characters, no spaces

        var parts = ChatReplyFormatter.Split(text);

        Assert.True(parts.Count >= 3);
        Assert.All(parts, p => Assert.True(p.Length <= TelegramLimit, $"part of {p.Length} characters"));
        Assert.Equal(text, string.Concat(parts.Select(WebUtility.HtmlDecode)));
    }

    [Fact]
    public void Escaping_that_makes_the_text_longer_still_fits_and_never_cuts_an_entity()
    {
        var text = new string('"', 5000) + new string('&', 5000); // every character grows to 5 or 6

        var parts = ChatReplyFormatter.Split(text);

        Assert.All(parts, p => Assert.True(p.Length <= TelegramLimit, $"part of {p.Length} characters"));
        Assert.All(parts, p => Assert.Equal(WebUtility.HtmlEncode(WebUtility.HtmlDecode(p)), p));
        Assert.Equal(text, string.Concat(parts.Select(WebUtility.HtmlDecode)));
    }

    [Fact]
    public void A_long_reply_is_cut_at_a_paragraph_when_there_is_one()
    {
        var first = new string('а', 3000);
        var second = new string('б', 3000);

        var parts = ChatReplyFormatter.Split(first + "\n\n" + second);

        Assert.Equal([first, second], parts);
    }
}
