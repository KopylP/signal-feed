using Microsoft.Extensions.Options;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using Telegram.Bot.Types.Enums;

namespace PostAgent.Tests.Shared.Notifications;

public class ChatResolverTests
{
    private static ChatResolver Resolver(long configured, params long[] bound) =>
        new(Options.Create(new AgentOptions { TelegramChatId = configured }), new InMemoryChatBindingStore(bound));

    [Fact]
    public async Task Uses_the_bound_chat_when_no_chat_is_configured()
    {
        Assert.Equal([42L], await Resolver(configured: 0, 42).GetChatIdsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Returns_every_bound_chat()
    {
        Assert.Equal([42L, 43L], await Resolver(configured: 0, 42, 43).GetChatIdsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_configured_chat_wins_over_the_bound_ones_and_means_exactly_one_chat()
    {
        Assert.Equal([7L], await Resolver(configured: 7, 42, 43).GetChatIdsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_chat_is_bound_only_when_it_is_in_the_list()
    {
        var resolver = Resolver(configured: 0, 42, 43);

        Assert.True(await resolver.IsBoundAsync(43, CancellationToken.None));
        Assert.False(await resolver.IsBoundAsync(44, CancellationToken.None));
    }

    [Fact]
    public async Task Fails_with_a_clear_message_when_nothing_is_bound()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Resolver(configured: 0).RequireChatIdsAsync(CancellationToken.None));

        Assert.Contains("/start", error.Message);
    }

    [Theory]
    [InlineData(42, ChatType.Private, true)]
    [InlineData(43, ChatType.Private, false)]
    [InlineData(42, ChatType.Group, false)]
    [InlineData(42, ChatType.Supergroup, false)]
    public async Task Serves_only_a_bound_private_chat(long chatId, ChatType type, bool expected)
    {
        var chat = new Telegram.Bot.Types.Chat { Id = chatId, Type = type };

        Assert.Equal(expected, await Resolver(configured: 0, 42).ServesAsync(chat, CancellationToken.None));
    }
}
