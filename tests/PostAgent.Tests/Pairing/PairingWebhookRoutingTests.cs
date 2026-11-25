using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PostAgent.Pairing;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;

namespace PostAgent.Tests.Pairing;

public class PairingWebhookRoutingTests
{
    private const string Key = "pairing-key-0123456789";
    private const string Secret = "webhook_secret-token";

    private const string CallbackUpdate =
        """{"update_id":2,"callback_query":{"id":"cb1","from":{"id":42,"is_bot":false,"first_name":"x"},"chat_instance":"1","data":"m","message":{"message_id":7,"date":1700000000,"chat":{"id":42,"type":"private"}}}}""";

    private const string TextUpdate =
        """{"update_id":3,"message":{"message_id":1,"date":1700000000,"chat":{"id":42,"type":"private"},"text":"hello"}}""";

    private static PairingWebhook Webhook(InMemoryChatBindingStore store, params IIncomingMessageHandler[] handlers)
    {
        var options = Options.Create(new AgentOptions { TelegramPairingKey = Key, TelegramWebhookSecret = Secret });
        var service = new PairingService(store, new RecordingNotifier(), options, NullLogger<PairingService>.Instance);
        return new PairingWebhook(service, handlers, options, NullLogger<PairingWebhook>.Instance);
    }

    private static MemoryStream Body(string text) => new(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task Gives_a_callback_query_to_the_handlers()
    {
        var handler = new RecordingIncomingHandler();

        var status = await Webhook(new InMemoryChatBindingStore(), handler).HandleAsync(Secret, Body(CallbackUpdate), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("cb1", Assert.Single(handler.Handled).CallbackQuery?.Id);
    }

    [Fact]
    public async Task Stops_at_the_first_handler_that_handled_the_update()
    {
        var first = new RecordingIncomingHandler();
        var second = new RecordingIncomingHandler();

        await Webhook(new InMemoryChatBindingStore(), first, second).HandleAsync(Secret, Body(TextUpdate), CancellationToken.None);

        Assert.Single(first.Handled);
        Assert.Empty(second.Handled);
    }

    [Fact]
    public async Task Offers_the_update_to_the_next_handler_when_the_first_does_not_claim_it()
    {
        var first = new RecordingIncomingHandler { Claims = false };
        var second = new RecordingIncomingHandler();

        await Webhook(new InMemoryChatBindingStore(), first, second).HandleAsync(Secret, Body(TextUpdate), CancellationToken.None);

        Assert.Single(first.Handled);
        Assert.Single(second.Handled);
    }

    [Fact]
    public async Task Still_sends_start_to_pairing_before_any_handler()
    {
        var handler = new RecordingIncomingHandler();
        var store = new InMemoryChatBindingStore();
        var start = $$$"""{"update_id":1,"message":{"message_id":1,"date":1700000000,"chat":{"id":42,"type":"private"},"text":"/start {{{Key}}}"}}""";

        await Webhook(store, handler).HandleAsync(Secret, Body(start), CancellationToken.None);

        Assert.Empty(handler.Handled);
        Assert.Equal([42L], store.Bound);
    }

    [Fact]
    public async Task Ignores_an_update_without_message_or_callback()
    {
        var handler = new RecordingIncomingHandler();
        const string edited = """{"update_id":4,"edited_message":{"message_id":1,"date":1700000000,"chat":{"id":42,"type":"private"},"text":"x"}}""";

        var status = await Webhook(new InMemoryChatBindingStore(), handler).HandleAsync(Secret, Body(edited), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Empty(handler.Handled);
    }
}
