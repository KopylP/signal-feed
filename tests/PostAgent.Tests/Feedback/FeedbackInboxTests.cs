using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Feedback;
using PostAgent.Shared.Chat;
using PostAgent.Shared.Feedback;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Feedback;

public class FeedbackInboxTests
{
    private const long BoundChat = 42;

    private readonly InMemoryChatStateStore _state = new();
    private readonly InMemoryFeedbackStore _store = new();
    private readonly RecordingPanel _panel = new();

    private static ReportSnapshot Report() => new("r1", Now, null,
    [
        new SnapshotEntry("Headlines", "2026-11-24/a_1", "chan", Now, "Оновлення Google", "Суть", null, "Уривок", 9, "https://t.me/chan/1"),
        new SnapshotEntry("Notable", "2026-11-24/b_2", "other", Now, null, "Суть", null, "Уривок", 7, "https://t.me/other/2"),
    ], []);

    private async Task<FeedbackInbox> Create(long? bound = BoundChat)
    {
        await _state.StartNewAsync(BoundChat, Report(), CancellationToken.None);
        var resolver = new ChatResolver(Microsoft.Extensions.Options.Options.Create(new AgentOptions()),
            bound is { } b ? new InMemoryChatBindingStore(b) : new InMemoryChatBindingStore());
        return new FeedbackInbox(resolver, _state, _store, _panel, new FakeTimeProvider(Now), NullLogger<FeedbackInbox>.Instance);
    }

    private static Update Press(string data, long chatId = BoundChat, ChatType type = ChatType.Private, InlineKeyboardMarkup? markup = null) => new()
    {
        Id = 1,
        CallbackQuery = new CallbackQuery
        {
            Id = "cb1",
            Data = data,
            Message = new Message { Id = 7, Chat = new Telegram.Bot.Types.Chat { Id = chatId, Type = type }, ReplyMarkup = markup },
        },
    };

    private static InlineKeyboardMarkup Markup() => new(
    [
        [InlineKeyboardButton.WithCallbackData("👍 Оновлення Google", FeedbackCallback.Encode("r1", 0, FeedbackVote.Up)), InlineKeyboardButton.WithCallbackData("👎", FeedbackCallback.Encode("r1", 0, FeedbackVote.Down))],
        [InlineKeyboardButton.WithCallbackData("👍 other", FeedbackCallback.Encode("r1", 1, FeedbackVote.Up)), InlineKeyboardButton.WithCallbackData("👎", FeedbackCallback.Encode("r1", 1, FeedbackVote.Down))],
    ]);

    [Fact]
    public async Task A_like_is_recorded_with_the_title_of_the_entry_and_thanked()
    {
        var inbox = await Create();

        var handled = await inbox.HandleAsync(Press(FeedbackCallback.Encode("r1", 0, FeedbackVote.Up)), CancellationToken.None);

        Assert.True(handled);
        var item = Assert.Single(_store.Items);
        Assert.Equal(("2026-11-24/a_1", "chan", "Оновлення Google", FeedbackVote.Up, Now), (item.PostId, item.Source, item.Topic, item.Vote, item.At));
        Assert.Equal(FeedbackTexts.ThanksUp, Assert.Single(_panel.Answered).Text);
    }

    [Fact]
    public async Task An_entry_without_a_title_is_recorded_by_the_start_of_its_text()
    {
        var inbox = await Create();

        await inbox.HandleAsync(Press(FeedbackCallback.Encode("r1", 1, FeedbackVote.Down)), CancellationToken.None);

        Assert.Equal("Суть", Assert.Single(_store.Items).Topic);
        Assert.Equal(FeedbackTexts.ThanksDown, Assert.Single(_panel.Answered).Text);
    }

    [Fact]
    public async Task Changing_the_mind_replaces_the_vote()
    {
        var inbox = await Create();

        await inbox.HandleAsync(Press(FeedbackCallback.Encode("r1", 0, FeedbackVote.Up)), CancellationToken.None);
        await inbox.HandleAsync(Press(FeedbackCallback.Encode("r1", 0, FeedbackVote.Down)), CancellationToken.None);

        Assert.Equal(FeedbackVote.Down, Assert.Single(_store.Items).Vote);
    }

    [Fact]
    public async Task The_pressed_button_gets_a_check_mark_and_the_rest_of_the_keyboard_stays()
    {
        var inbox = await Create();

        await inbox.HandleAsync(Press(FeedbackCallback.Encode("r1", 1, FeedbackVote.Up), markup: Markup()), CancellationToken.None);

        var edited = Assert.Single(_panel.Edited);
        Assert.Equal(FeedbackPrompt.Message, edited.Html);
        Assert.Equal(["👍 Оновлення Google", "👎"], edited.Keyboard.Rows[0].Select(b => b.Text));
        Assert.Equal(["✅ 👍 other", "👎"], edited.Keyboard.Rows[1].Select(b => b.Text));
    }

    [Fact]
    public async Task A_long_title_is_cut_before_it_is_stored()
    {
        var inbox = await Create();
        await _state.StartNewAsync(BoundChat, new ReportSnapshot("r1", Now, null,
            [new SnapshotEntry("Headlines", "2026-11-24/a_1", "chan", Now, new string('я', 300), "Суть", null, "Уривок", 9, "https://t.me/chan/1")], []), CancellationToken.None);

        await inbox.HandleAsync(Press(FeedbackCallback.Encode("r1", 0, FeedbackVote.Up)), CancellationToken.None);

        Assert.Equal(FeedbackLog.MaxTopicChars + 1, Assert.Single(_store.Items).Topic.Length); // the cut text and the ellipsis
    }

    [Fact]
    public async Task Marks_of_the_other_entries_stay_when_another_one_is_rated()
    {
        var inbox = await Create();
        var rated = new InlineKeyboardMarkup(
        [
            [InlineKeyboardButton.WithCallbackData("✅ 👍 Оновлення Google", FeedbackCallback.Encode("r1", 0, FeedbackVote.Up)), InlineKeyboardButton.WithCallbackData("👎", FeedbackCallback.Encode("r1", 0, FeedbackVote.Down))],
            [InlineKeyboardButton.WithCallbackData("👍 other", FeedbackCallback.Encode("r1", 1, FeedbackVote.Up)), InlineKeyboardButton.WithCallbackData("👎", FeedbackCallback.Encode("r1", 1, FeedbackVote.Down))],
        ]);

        await inbox.HandleAsync(Press(FeedbackCallback.Encode("r1", 1, FeedbackVote.Down), markup: rated), CancellationToken.None);

        var rows = Assert.Single(_panel.Edited).Keyboard.Rows;
        Assert.Equal(["✅ 👍 Оновлення Google", "👎"], rows[0].Select(b => b.Text));
        Assert.Equal(["👍 other", "✅ 👎"], rows[1].Select(b => b.Text));
    }

    [Fact]
    public async Task Changing_the_vote_moves_the_mark_within_its_row()
    {
        var inbox = await Create();
        var rated = new InlineKeyboardMarkup(
        [
            [InlineKeyboardButton.WithCallbackData("✅ 👍 Оновлення Google", FeedbackCallback.Encode("r1", 0, FeedbackVote.Up)), InlineKeyboardButton.WithCallbackData("👎", FeedbackCallback.Encode("r1", 0, FeedbackVote.Down))],
        ]);

        await inbox.HandleAsync(Press(FeedbackCallback.Encode("r1", 0, FeedbackVote.Down), markup: rated), CancellationToken.None);

        Assert.Equal(["👍 Оновлення Google", "✅ 👎"], Assert.Single(_panel.Edited).Keyboard.Rows[0].Select(b => b.Text));
    }

    [Fact]
    public async Task A_spinner_that_cannot_be_stopped_does_not_undo_the_vote_or_the_mark()
    {
        var inbox = await Create();
        _panel.FailAnswers = true;

        var handled = await inbox.HandleAsync(Press(FeedbackCallback.Encode("r1", 0, FeedbackVote.Up), markup: Markup()), CancellationToken.None);

        Assert.True(handled);
        Assert.Single(_store.Items);
        Assert.Single(_panel.Edited);
    }

    [Fact]
    public async Task A_failed_edit_does_not_undo_the_vote()
    {
        var inbox = await Create();
        _panel.FailEdits = true;

        await inbox.HandleAsync(Press(FeedbackCallback.Encode("r1", 0, FeedbackVote.Up), markup: Markup()), CancellationToken.None);

        Assert.Single(_store.Items);
    }

    [Fact]
    public async Task A_button_of_an_older_digest_is_told_so_and_records_nothing()
    {
        var inbox = await Create();

        await inbox.HandleAsync(Press(FeedbackCallback.Encode("old", 0, FeedbackVote.Up)), CancellationToken.None);

        Assert.Empty(_store.Items);
        Assert.Equal(FeedbackTexts.Outdated, Assert.Single(_panel.Answered).Text);
    }

    [Fact]
    public async Task An_entry_number_outside_the_digest_records_nothing()
    {
        var inbox = await Create();

        await inbox.HandleAsync(Press(FeedbackCallback.Encode("r1", 9, FeedbackVote.Up)), CancellationToken.None);

        Assert.Empty(_store.Items);
        Assert.Equal(FeedbackTexts.Outdated, Assert.Single(_panel.Answered).Text);
    }

    [Theory]
    [InlineData(99L, ChatType.Private)]
    [InlineData(BoundChat, ChatType.Group)]
    public async Task A_press_from_another_chat_or_a_group_is_swallowed_and_records_nothing(long chatId, ChatType type)
    {
        var inbox = await Create();

        var handled = await inbox.HandleAsync(Press(FeedbackCallback.Encode("r1", 0, FeedbackVote.Up), chatId, type), CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(_store.Items);
    }

    [Theory]
    [InlineData("m")]
    [InlineData("pa:123")]
    public async Task Buttons_of_the_settings_menu_are_left_to_the_menu(string data)
    {
        var inbox = await Create();

        Assert.False(await inbox.HandleAsync(Press(data), CancellationToken.None));
        Assert.Empty(_panel.Answered);
    }

    [Fact]
    public async Task A_text_message_is_left_to_the_others()
    {
        var inbox = await Create();

        Assert.False(await inbox.HandleAsync(new Update { Id = 2, Message = new Message { Text = "привіт", Chat = new Telegram.Bot.Types.Chat { Id = BoundChat, Type = ChatType.Private } } }, CancellationToken.None));
    }

    [Fact]
    public async Task A_storage_failure_is_answered_and_not_thrown()
    {
        var inbox = await Create();
        _store.ThrowOnRecord = true;

        var handled = await inbox.HandleAsync(Press(FeedbackCallback.Encode("r1", 0, FeedbackVote.Up), markup: Markup()), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(FeedbackTexts.Failed, Assert.Single(_panel.Answered).Text);
        Assert.Empty(_panel.Edited);
    }
}
