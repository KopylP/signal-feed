using Microsoft.Extensions.Options;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;

namespace PostAgent.Tests.Shared.Notifications;

public class ChatResolverTests
{
    private static ChatResolver Resolver(long configured, long? bound) =>
        new(Options.Create(new AgentOptions { TelegramChatId = configured }), new InMemoryChatBindingStore(bound));

    [Fact]
    public async Task Uses_the_bound_chat_when_no_chat_is_configured()
    {
        Assert.Equal(42, await Resolver(configured: 0, bound: 42).GetChatIdAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_configured_chat_wins_over_the_bound_one()
    {
        Assert.Equal(7, await Resolver(configured: 7, bound: 42).GetChatIdAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Fails_with_a_clear_message_when_nothing_is_bound()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Resolver(configured: 0, bound: null).GetChatIdAsync(CancellationToken.None));

        Assert.Contains("/start", error.Message);
    }
}
