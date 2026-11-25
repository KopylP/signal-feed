using Microsoft.Extensions.Logging.Abstractions;
using PostAgent.Chat;
using PostAgent.Shared.Chat;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Chat;

public class ChatInboxTests
{
    private const long BoundChat = 42;

    private readonly InMemoryChatStateStore _state = new();
    private readonly FakeChatQueue _queue = new();
    private readonly RecordingNotifier _notifier = new();

    private async Task<ChatInbox> Create(long? bound = BoundChat, bool withReport = true, FakeChatQueue? queue = null)
    {
        if (withReport)
        {
            await _state.StartNewAsync(new ReportSnapshot("r1", Now, null, [], []), CancellationToken.None);
        }

        var options = Microsoft.Extensions.Options.Options.Create(new AgentOptions());
        var resolver = new ChatResolver(options, new InMemoryChatBindingStore(bound));
        return new ChatInbox(resolver, _state, queue ?? _queue, _notifier, NullLogger<ChatInbox>.Instance);
    }

    private static Update Incoming(string? text, long chatId = BoundChat, ChatType type = ChatType.Private, int updateId = 100) =>
        new() { Id = updateId, Message = new Message { Chat = new Telegram.Bot.Types.Chat { Id = chatId, Type = type }, Text = text } };

    [Fact]
    public async Task A_text_message_from_the_bound_private_chat_is_queued_with_the_report_id()
    {
        var inbox = await Create();

        await inbox.HandleAsync(Incoming("Розкажи про пункт 3", updateId: 777), CancellationToken.None);

        Assert.Equal(new ChatRequest(777, "Розкажи про пункт 3", "r1"), Assert.Single(_queue.Enqueued));
        Assert.Empty(_notifier.Sent);
    }

    [Theory]
    [InlineData(99L, ChatType.Private, "hello")]    // another chat
    [InlineData(BoundChat, ChatType.Group, "hello")] // the bound id, but a group
    [InlineData(BoundChat, ChatType.Private, null)]  // a sticker or a photo has no text
    [InlineData(BoundChat, ChatType.Private, "   ")]
    [InlineData(BoundChat, ChatType.Private, "/start some-key")]
    [InlineData(BoundChat, ChatType.Private, "/start")]
    [InlineData(BoundChat, ChatType.Private, "/help")]
    public async Task Everything_else_is_ignored_without_a_reply(long chatId, ChatType type, string? text)
    {
        var inbox = await Create();

        await inbox.HandleAsync(Incoming(text, chatId, type), CancellationToken.None);

        Assert.Empty(_queue.Enqueued);
        Assert.Empty(_notifier.Sent);
    }

    [Fact]
    public async Task An_update_that_is_not_a_message_is_ignored()
    {
        var inbox = await Create();

        await inbox.HandleAsync(new Update { Id = 5 }, CancellationToken.None);

        Assert.Empty(_queue.Enqueued);
    }

    [Fact]
    public async Task Long_text_is_cut_to_2000_characters()
    {
        var inbox = await Create();

        await inbox.HandleAsync(Incoming(new string('я', 5000)), CancellationToken.None);

        Assert.Equal(2000, Assert.Single(_queue.Enqueued).Text.Length);
    }

    [Fact]
    public async Task Nothing_is_answered_while_no_chat_is_bound()
    {
        var inbox = await Create(bound: null);

        await inbox.HandleAsync(Incoming("hello"), CancellationToken.None);

        Assert.Empty(_queue.Enqueued);
        Assert.Empty(_notifier.Sent);
    }

    [Fact]
    public async Task Without_a_digest_the_reader_is_told_so_and_nothing_is_queued()
    {
        var inbox = await Create(withReport: false);

        await inbox.HandleAsync(Incoming("hello"), CancellationToken.None);

        Assert.Equal([ChatTexts.NoReport], _notifier.Sent);
        Assert.Empty(_queue.Enqueued);
    }

    [Fact]
    public async Task A_queue_failure_is_swallowed()
    {
        var inbox = await Create(queue: new FakeChatQueue { Fail = true });

        await inbox.HandleAsync(Incoming("hello"), CancellationToken.None);

        Assert.Empty(_notifier.Sent);
    }
}
