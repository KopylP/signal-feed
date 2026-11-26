using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Digest;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Digest;

public class DigestInboxTests
{
    private const long BoundChat = 42;

    private readonly FakeDigestRequestQueue _queue = new();
    private readonly RecordingNotifier _notifier = new();

    private DigestInbox Create(long? bound = BoundChat, FakeDigestRequestQueue? queue = null, Action<AgentOptions>? configure = null)
    {
        var app = new AgentOptions { TimeZone = "UTC" };
        configure?.Invoke(app);
        var resolver = new ChatResolver(Microsoft.Extensions.Options.Options.Create(app),
            bound is { } b ? new InMemoryChatBindingStore(b) : new InMemoryChatBindingStore());
        return new DigestInbox(resolver, queue ?? _queue, _notifier, new FakeEffectiveOptions(app), new FakeTimeProvider(Now), NullLogger<DigestInbox>.Instance);
    }

    private static Update Incoming(string? text, long chatId = BoundChat, ChatType type = ChatType.Private) =>
        new() { Id = 1, Message = new Message { Chat = new Telegram.Bot.Types.Chat { Id = chatId, Type = type }, Text = text } };

    [Fact]
    public async Task A_command_without_arguments_queues_the_last_day_for_the_asking_chat_and_says_so()
    {
        var handled = await Create().HandleAsync(Incoming("/digest"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(new DigestRequest(BoundChat, Now.AddHours(-24), Now), Assert.Single(_queue.Enqueued));
        var reply = Assert.Single(_notifier.SentTo);
        Assert.Equal(BoundChat, reply.ChatId);
        Assert.StartsWith("⏳ Готую дайджест", reply.Html);
    }

    [Theory]
    [InlineData("/digest 6")]
    [InlineData("/DIGEST@my_bot 6")]
    [InlineData("  /digest   6h  ")]
    public async Task The_arguments_and_the_bot_name_in_the_command_are_understood(string text)
    {
        await Create().HandleAsync(Incoming(text), CancellationToken.None);

        Assert.Equal(Now.AddHours(-6), Assert.Single(_queue.Enqueued).Since);
    }

    [Theory]
    [InlineData("/digest nonsense")]
    [InlineData("/digest 0")]
    public async Task An_unreadable_period_gets_the_usage_and_queues_nothing(string text)
    {
        var handled = await Create().HandleAsync(Incoming(text), CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(_queue.Enqueued);
        Assert.Equal(DigestTexts.Usage, Assert.Single(_notifier.Replies));
    }

    [Theory]
    [InlineData("привіт")]
    [InlineData("/clear")]
    [InlineData("/digestive")]
    [InlineData("/settings digest")]
    public async Task Other_texts_are_left_to_the_others(string text)
    {
        Assert.False(await Create().HandleAsync(Incoming(text), CancellationToken.None));
        Assert.Empty(_queue.Enqueued);
    }

    [Theory]
    [InlineData(99L, ChatType.Private)]
    [InlineData(BoundChat, ChatType.Group)]
    public async Task Another_chat_or_a_group_is_left_to_the_others(long chatId, ChatType type)
    {
        Assert.False(await Create().HandleAsync(Incoming("/digest", chatId, type), CancellationToken.None));
        Assert.Empty(_queue.Enqueued);
        Assert.Empty(_notifier.SentTo);
    }

    [Fact]
    public async Task A_queue_failure_is_logged_and_not_thrown()
    {
        var handled = await Create(queue: new FakeDigestRequestQueue { Fail = true }).HandleAsync(Incoming("/digest"), CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(_notifier.SentTo);
    }
}
