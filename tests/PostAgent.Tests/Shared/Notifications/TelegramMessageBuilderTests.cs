using PostAgent.Shared.Notifications;

namespace PostAgent.Tests.Shared.Notifications;

public class TelegramMessageBuilderTests
{
    [Fact]
    public void Blocks_that_fit_go_into_one_message_separated_by_a_blank_line()
    {
        var messages = new TelegramMessageBuilder(100).Add("intro").Section("H", ["a", "b"]).Build();

        Assert.Equal(["intro\n\nH\n\na\n\nb"], messages);
    }

    [Fact]
    public void A_section_without_blocks_writes_nothing()
    {
        var messages = new TelegramMessageBuilder(100).Add("intro").Section("H", []).Build();

        Assert.Equal(["intro"], messages);
    }

    [Fact]
    public void A_block_that_does_not_fit_starts_a_new_message_and_the_section_header_is_repeated()
    {
        var block = new string('x', 30);

        var messages = new TelegramMessageBuilder(50).Section("H", [block, block]).Build();

        Assert.Equal([$"H\n\n{block}", $"H (далі)\n\n{block}"], messages);
        Assert.All(messages, m => Assert.True(m.Length <= 50));
    }

    [Fact]
    public void Nothing_added_builds_no_messages()
    {
        Assert.Empty(new TelegramMessageBuilder(100).Build());
    }
}
