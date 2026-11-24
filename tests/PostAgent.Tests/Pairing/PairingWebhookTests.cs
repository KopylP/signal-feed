using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PostAgent.Pairing;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;

namespace PostAgent.Tests.Pairing;

public class PairingWebhookTests
{
    private const string Key = "pairing-key-0123456789";
    private const string Secret = "webhook_secret-token";

    private static string StartUpdate(string key = Key) =>
        $$$"""{"update_id":1,"message":{"message_id":1,"date":1700000000,"chat":{"id":42,"type":"private"},"text":"/start {{{key}}}"}}""";

    private static PairingWebhook Webhook(IChatBindingStore store, RecordingNotifier? notifier = null)
    {
        var options = Options.Create(new AgentOptions { TelegramPairingKey = Key, TelegramWebhookSecret = Secret });
        var service = new PairingService(store, notifier ?? new RecordingNotifier(), options, NullLogger<PairingService>.Instance);
        return new PairingWebhook(service, options, NullLogger<PairingWebhook>.Instance);
    }

    private static MemoryStream Body(string text) => new(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task Binds_the_chat_from_a_real_telegram_update()
    {
        var store = new InMemoryChatBindingStore();

        var status = await Webhook(store).HandleAsync(Secret, Body(StartUpdate()), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(42, store.Bound);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong")]
    public async Task Rejects_a_request_without_the_webhook_secret_and_does_not_read_it(string? token)
    {
        var store = new InMemoryChatBindingStore();

        var status = await Webhook(store).HandleAsync(token, Body(StartUpdate()), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, status);
        Assert.Null(store.Bound);
    }

    [Fact]
    public async Task A_wrong_secret_is_rejected_before_the_body_is_read()
    {
        var status = await Webhook(new InMemoryChatBindingStore()).HandleAsync("wrong", new UnreadableStream(), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, status);
    }

    [Fact]
    public async Task A_body_over_the_limit_is_rejected_without_parsing()
    {
        var store = new InMemoryChatBindingStore();
        var huge = new MemoryStream(new byte[PairingWebhook.MaxBodyBytes + 1]);

        var status = await Webhook(store).HandleAsync(Secret, huge, CancellationToken.None);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, status);
        Assert.Null(store.Bound);
    }

    [Fact]
    public async Task Rejects_a_body_that_is_not_an_update()
    {
        var status = await Webhook(new InMemoryChatBindingStore()).HandleAsync(Secret, Body("not json"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Fact]
    public async Task Answers_ok_to_updates_that_are_not_messages()
    {
        var status = await Webhook(new InMemoryChatBindingStore()).HandleAsync(Secret, Body("""{"update_id":2}"""), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Fact]
    public async Task A_storage_failure_does_not_make_telegram_resend_the_update()
    {
        var status = await Webhook(new FailingStore()).HandleAsync(Secret, Body(StartUpdate()), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Fact]
    public async Task Cancellation_is_not_swallowed()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Webhook(new CancellingStore()).HandleAsync(Secret, Body(StartUpdate()), cts.Token));
    }

    private sealed class UnreadableStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The body must not be read before the secret is checked.");
    }

    private sealed class FailingStore : IChatBindingStore
    {
        public Task<long?> GetAsync(CancellationToken ct) => throw new InvalidOperationException("storage is down");

        public Task<bool> TryBindAsync(long chatId, CancellationToken ct) => throw new InvalidOperationException("storage is down");
    }

    private sealed class CancellingStore : IChatBindingStore
    {
        public Task<long?> GetAsync(CancellationToken ct) => throw new OperationCanceledException(ct);

        public Task<bool> TryBindAsync(long chatId, CancellationToken ct) => throw new OperationCanceledException(ct);
    }
}
