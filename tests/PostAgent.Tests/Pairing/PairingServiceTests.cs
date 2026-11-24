using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PostAgent.Pairing;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PostAgent.Tests.Pairing;

public class PairingServiceTests
{
    private const string Key = "pairing-key-0123456789";

    private readonly RecordingNotifier _notifier = new();

    private PairingService Service(InMemoryChatBindingStore store, long configuredChat = 0) =>
        new(store, _notifier, Options.Create(new AgentOptions { TelegramPairingKey = Key, TelegramChatId = configuredChat }),
            NullLogger<PairingService>.Instance);

    private static Message Start(long chatId, string key = Key, ChatType type = ChatType.Private, string? text = null) =>
        new() { Chat = new Chat { Id = chatId, Type = type }, Text = text ?? $"/start {key}" };

    [Fact]
    public async Task Binds_the_chat_and_confirms_when_the_key_matches()
    {
        var store = new InMemoryChatBindingStore();

        await Service(store).HandleAsync(Start(42), CancellationToken.None);

        Assert.Equal(42, store.Bound);
        Assert.Equal([PairingMessage.Paired], _notifier.Sent);
    }

    [Fact]
    public async Task Ignores_a_wrong_key_without_replying()
    {
        var store = new InMemoryChatBindingStore();

        await Service(store).HandleAsync(Start(42, key: "guess"), CancellationToken.None);

        Assert.Null(store.Bound);
        Assert.Empty(_notifier.Sent);
    }

    [Fact]
    public async Task Ignores_messages_that_are_not_the_start_command()
    {
        var store = new InMemoryChatBindingStore();

        await Service(store).HandleAsync(Start(42, text: "hello"), CancellationToken.None);

        Assert.Null(store.Bound);
        Assert.Empty(_notifier.Sent);
    }

    [Theory]
    [InlineData(ChatType.Group)]
    [InlineData(ChatType.Supergroup)]
    [InlineData(ChatType.Channel)]
    public async Task Only_a_private_chat_can_be_bound(ChatType type)
    {
        var store = new InMemoryChatBindingStore();

        await Service(store).HandleAsync(Start(-100, type: type), CancellationToken.None);

        Assert.Null(store.Bound);
        Assert.Empty(_notifier.Sent);
    }

    [Fact]
    public async Task Another_chat_cannot_take_over_a_bound_agent_even_with_the_right_key()
    {
        var store = new InMemoryChatBindingStore(bound: 42);

        await Service(store).HandleAsync(Start(99), CancellationToken.None);

        Assert.Equal(42, store.Bound);
        Assert.Empty(_notifier.Sent);
    }

    [Fact]
    public async Task The_bound_chat_gets_a_repeated_confirmation()
    {
        var store = new InMemoryChatBindingStore(bound: 42);

        await Service(store).HandleAsync(Start(42), CancellationToken.None);

        Assert.Equal(42, store.Bound);
        Assert.Equal([PairingMessage.AlreadyPaired], _notifier.Sent);
    }

    [Fact]
    public async Task Pairing_is_off_while_a_chat_id_is_configured()
    {
        var store = new InMemoryChatBindingStore();

        await Service(store, configuredChat: 7).HandleAsync(Start(42), CancellationToken.None);

        Assert.Null(store.Bound);
        Assert.Empty(_notifier.Sent);
    }

    [Fact]
    public async Task A_message_without_text_or_chat_is_ignored()
    {
        var store = new InMemoryChatBindingStore();

        await Service(store).HandleAsync(new Message { Chat = new Chat { Id = 42, Type = ChatType.Private } }, CancellationToken.None);

        Assert.Null(store.Bound);
    }
}
