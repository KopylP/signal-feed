using Microsoft.Extensions.Logging.Abstractions;
using PostAgent.Chat;
using PostAgent.Chat.Intake;
using PostAgent.Shared.Chat;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Chat.Intake;

public class ClearInboxTests
{
    private const long BoundChat = 42;

    private readonly InMemoryChatStateStore _state = new();
    private readonly RecordingNotifier _notifier = new();

    private ClearInbox Create(params long[] bound) =>
        new(new ChatResolver(Microsoft.Extensions.Options.Options.Create(new AgentOptions()), new InMemoryChatBindingStore(bound.Length == 0 ? [BoundChat] : bound)),
            _state, _notifier, NullLogger<ClearInbox>.Instance);

    private static Update Incoming(string? text, long chatId = BoundChat, ChatType type = ChatType.Private) =>
        new() { Id = 1, Message = new Message { Chat = new Telegram.Bot.Types.Chat { Id = chatId, Type = type }, Text = text } };

    private async Task SeedAsync(long chatId, string reportId = "r1")
    {
        await _state.StartNewAsync(chatId, new ReportSnapshot(reportId, Now, "Огляд", [], []), CancellationToken.None);
        var handle = (await _state.LoadAsync(chatId, CancellationToken.None))!;
        await _state.TrySaveAsync(chatId, handle, handle.State.WithProcessed(7, FakeChatAgent.Session()), CancellationToken.None);
    }

    [Theory]
    [InlineData("/clear")]
    [InlineData("/clear@SeoFeedBot")]
    [InlineData("  /CLEAR ")]
    public async Task The_command_forgets_the_conversation_and_keeps_the_digest(string text)
    {
        await SeedAsync(BoundChat);

        var handled = await Create().HandleAsync(Incoming(text), CancellationToken.None);

        Assert.True(handled);
        var state = _state.Of(BoundChat)!;
        Assert.Null(state.Session);
        Assert.Equal("Огляд", state.Report.Overview);
        Assert.Equal([7L], state.ProcessedUpdateIds);
        Assert.Equal([(BoundChat, ChatTexts.Cleared)], _notifier.SentTo);
    }

    [Fact]
    public async Task With_nothing_to_forget_the_reader_still_gets_the_answer()
    {
        var handled = await Create().HandleAsync(Incoming("/clear"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal([(BoundChat, ChatTexts.Cleared)], _notifier.SentTo);
    }

    [Fact]
    public async Task Only_the_conversation_of_the_chat_that_typed_it_is_cleared()
    {
        await SeedAsync(BoundChat);
        await SeedAsync(43, "r2");

        await Create(BoundChat, 43).HandleAsync(Incoming("/clear", chatId: 43), CancellationToken.None);

        Assert.NotNull(_state.Of(BoundChat)!.Session);
        Assert.Null(_state.Of(43)!.Session);
    }

    [Fact]
    public async Task A_chat_that_is_not_paired_and_a_group_are_ignored()
    {
        await SeedAsync(BoundChat);
        await SeedAsync(99, "r3");

        Assert.False(await Create().HandleAsync(Incoming("/clear", chatId: 99), CancellationToken.None));
        Assert.False(await Create().HandleAsync(Incoming("/clear", type: ChatType.Group), CancellationToken.None));

        Assert.NotNull(_state.Of(BoundChat)!.Session);
        Assert.NotNull(_state.Of(99)!.Session);
        Assert.Empty(_notifier.SentTo);
    }

    [Theory]
    [InlineData("clear")]
    [InlineData("/clearall")]
    [InlineData("/settings")]
    [InlineData(null)]
    public async Task Other_messages_are_left_to_the_other_handlers(string? text)
    {
        Assert.False(await Create().HandleAsync(Incoming(text), CancellationToken.None));
    }

    [Fact]
    public async Task When_the_conversation_keeps_changing_the_reader_is_told_it_failed()
    {
        await SeedAsync(BoundChat);
        var n = 100;
        Action? hook = null;
        hook = () =>
        {
            _state.BeforeSave = null;
            var handle = _state.LoadAsync(BoundChat, CancellationToken.None).GetAwaiter().GetResult()!;
            _state.TrySaveAsync(BoundChat, handle, handle.State.WithProcessed(n++, FakeChatAgent.Session()), CancellationToken.None).GetAwaiter().GetResult();
            _state.BeforeSave = hook;
        };
        _state.BeforeSave = hook;

        var handled = await Create().HandleAsync(Incoming("/clear"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal([(BoundChat, ChatTexts.Failed)], _notifier.SentTo);
    }
}
