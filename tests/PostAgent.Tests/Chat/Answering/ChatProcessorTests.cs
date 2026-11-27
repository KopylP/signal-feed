using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Chat;
using PostAgent.Chat.Agent;
using PostAgent.Chat.Answering;
using PostAgent.Chat.Intake;
using PostAgent.Shared.Chat;
using PostAgent.Shared.Notifications;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Chat.Answering;

public class ChatProcessorTests
{
    private readonly InMemoryChatStateStore _state = new();
    private RecordingNotifier _notifier = new();

    private static ReportSnapshot Report(string id = "r1") => new(id, Now, "Огляд", [], []);

    private static ChatRequest Request(long updateId, string text = "питання", string reportId = "r1", long chatId = TestData.ChatId) => new(updateId, text, reportId, chatId);

    private ChatProcessor Create(FakeChatAgent agent) =>
        new(_state, agent, new ChatDelivery(_notifier, new FakeTimeProvider(Now), NullLogger<ChatDelivery>.Instance), new PostAgent.Tests.SettingsMenu.MenuHarness().ProposalSender, NullLogger<ChatProcessor>.Instance);

    private async Task Started(string id = "r1") => await _state.StartNewAsync(TestData.ChatId, Report(id), CancellationToken.None);

    [Fact]
    public async Task Replies_with_the_answer_and_remembers_the_update()
    {
        await Started();
        var agent = new FakeChatAgent(text => "відповідь на " + text);

        await Create(agent).HandleAsync(Request(10), CancellationToken.None);

        Assert.Equal(["відповідь на питання"], _notifier.Replies);
        Assert.Equal([10L], _state.Current?.ProcessedUpdateIds);
        Assert.NotNull(_state.Current?.Session);
    }

    [Fact]
    public async Task The_choices_of_the_agent_come_as_buttons_under_the_answer()
    {
        await Started();
        var agent = new FakeChatAgent((_, _) => Task.FromResult(new ChatReply("Яке джерело видалити?", FakeChatAgent.Session(), Choices: ["@one", "@two"])));

        await Create(agent).HandleAsync(Request(10), CancellationToken.None);

        Assert.Equal(["Яке джерело видалити?"], _notifier.Replies);
        Assert.Equal([["@one", "@two"]], _notifier.ReplyKeyboards.Single()!.Rows);
    }

    [Fact]
    public async Task A_change_of_the_reader_memory_is_told_under_the_answer_and_not_kept_in_the_conversation()
    {
        await Started();
        var agent = new FakeChatAgent((_, _) => Task.FromResult(new ChatReply("Добре", FakeChatAgent.Session(), MemoryNotices: ["🧠 Запам'ятав: «Робота»"])));

        await Create(agent).HandleAsync(Request(10), CancellationToken.None);

        var sent = Assert.Single(_notifier.Replies);
        Assert.StartsWith("Добре\n\n", sent);
        Assert.Contains("Запам&#39;ятав", sent);
    }

    [Fact]
    public async Task An_answer_without_choices_takes_away_the_buttons_of_an_earlier_one()
    {
        await Started();

        await Create(new FakeChatAgent(_ => "відповідь")).HandleAsync(Request(10), CancellationToken.None);

        Assert.Equal(ReplyKeyboard.Remove, _notifier.ReplyKeyboards.Single());
    }

    [Fact]
    public async Task Only_the_last_part_of_a_long_answer_carries_the_choices()
    {
        await Started();
        var text = string.Join("\n\n", Enumerable.Repeat(new string('a', 1000), 6));
        var agent = new FakeChatAgent((_, _) => Task.FromResult(new ChatReply(text, FakeChatAgent.Session(), Choices: ["@one"])));

        await Create(agent).HandleAsync(Request(10), CancellationToken.None);

        Assert.True(_notifier.ReplyKeyboards.Count > 1);
        Assert.All(_notifier.ReplyKeyboards.SkipLast(1), k => Assert.Null(k));
        Assert.Equal(["@one"], _notifier.ReplyKeyboards[^1]!.Rows.Single());
    }

    [Fact]
    public async Task A_reply_that_clears_the_context_is_sent_and_the_conversation_is_saved_without_a_session()
    {
        await Started();
        var agent = new FakeChatAgent((_, _) => Task.FromResult(new ChatReply("Забув", FakeChatAgent.Session(), ClearsContext: true)));

        await Create(agent).HandleAsync(Request(10), CancellationToken.None);

        Assert.Equal(["Забув"], _notifier.Replies);
        Assert.Null(_state.Current?.Session);
        Assert.Equal([10L], _state.Current?.ProcessedUpdateIds);
        Assert.Equal("Огляд", _state.Current?.Report.Overview);
    }

    [Fact]
    public async Task Passes_the_time_the_reader_wrote_to_the_agent()
    {
        await Started();
        var agent = new FakeChatAgent(_ => "відповідь");
        var sent = new DateTimeOffset(2026, 11, 24, 7, 55, 0, TimeSpan.Zero);

        await Create(agent).HandleAsync(Request(10) with { SentAt = sent }, CancellationToken.None);

        Assert.Equal(sent, agent.LastSentAt);
    }

    [Fact]
    public async Task A_redelivered_update_is_answered_once()
    {
        await Started();
        var agent = new FakeChatAgent(_ => "відповідь");
        var processor = Create(agent);

        await processor.HandleAsync(Request(10), CancellationToken.None);
        await processor.HandleAsync(Request(10), CancellationToken.None);

        Assert.Single(_notifier.Replies);
        Assert.Equal(1, agent.Calls);
    }

    [Fact]
    public async Task A_lower_update_id_saved_after_a_higher_one_still_gets_its_reply()
    {
        await Started();
        ChatProcessor? processor = null;
        var agent = new FakeChatAgent(async (_, text) =>
        {
            if (text == "A")
            {
                // While A is thinking, B (a higher id) is handled completely and saves first.
                await processor!.HandleAsync(Request(11, "B"), CancellationToken.None);
            }

            return new ChatReply("ans:" + text, FakeChatAgent.Session());
        });
        processor = Create(agent);

        await processor.HandleAsync(Request(10, "A"), CancellationToken.None);

        Assert.Equal(["ans:B", "ans:A"], _notifier.Replies);
        Assert.Equal([11L, 10L], _state.Current?.ProcessedUpdateIds);
    }

    [Fact]
    public async Task The_same_update_handled_by_another_worker_meanwhile_is_not_answered_twice()
    {
        await Started();
        ChatProcessor? processor = null;
        var nested = false;
        var agent = new FakeChatAgent(async (_, text) =>
        {
            if (!nested)
            {
                nested = true;
                await processor!.HandleAsync(Request(10, text), CancellationToken.None);
            }

            return new ChatReply("ans", FakeChatAgent.Session());
        });
        processor = Create(agent);

        await processor.HandleAsync(Request(10), CancellationToken.None);

        Assert.Equal(["ans"], _notifier.Replies);
    }

    [Fact]
    public async Task A_new_digest_during_the_turn_drops_the_reply_and_keeps_the_new_conversation()
    {
        await Started();
        var agent = new FakeChatAgent(async (_, _) =>
        {
            await _state.StartNewAsync(TestData.ChatId, Report("r2"), CancellationToken.None);
            return new ChatReply("стара відповідь", FakeChatAgent.Session());
        });

        await Create(agent).HandleAsync(Request(10), CancellationToken.None);

        Assert.Empty(_notifier.Replies);
        Assert.Equal("r2", _state.Current?.Report.ReportId);
        Assert.Empty(_state.Current!.ProcessedUpdateIds);
    }

    [Fact]
    public async Task A_conversation_deleted_during_the_turn_drops_the_reply()
    {
        await Started();
        var agent = new FakeChatAgent((_, _) =>
        {
            _state.Clear();
            return Task.FromResult(new ChatReply("відповідь", FakeChatAgent.Session()));
        });

        await Create(agent).HandleAsync(Request(10), CancellationToken.None);

        Assert.Empty(_notifier.Replies);
        Assert.Null(_state.Current);
    }

    [Fact]
    public async Task A_message_queued_before_the_digest_is_dropped_without_calling_the_model()
    {
        await Started("r2");
        var agent = new FakeChatAgent(_ => "відповідь");

        await Create(agent).HandleAsync(Request(10, reportId: "r1"), CancellationToken.None);

        Assert.Equal(0, agent.Calls);
        Assert.Empty(_notifier.Replies);
    }

    [Fact]
    public async Task With_no_conversation_the_message_is_dropped()
    {
        var agent = new FakeChatAgent(_ => "відповідь");

        await Create(agent).HandleAsync(Request(10), CancellationToken.None);

        Assert.Equal(0, agent.Calls);
        Assert.Empty(_notifier.Replies);
    }

    [Fact]
    public async Task Gives_up_after_three_attempts_when_the_state_keeps_changing_and_sends_nothing()
    {
        await Started();
        var agent = new FakeChatAgent(async (_, _) =>
        {
            await _state.StartNewAsync(TestData.ChatId, Report(), CancellationToken.None); // same report, new version
            return new ChatReply("відповідь", FakeChatAgent.Session());
        });

        await Create(agent).HandleAsync(Request(10), CancellationToken.None);

        Assert.Equal(3, agent.Calls);
        Assert.Empty(_notifier.Replies);
    }

    [Fact]
    public async Task A_model_failure_sends_one_short_message_and_saves_nothing()
    {
        await Started();
        var agent = new FakeChatAgent((_, _) => throw new InvalidOperationException("model is down"));

        await Create(agent).HandleAsync(Request(10), CancellationToken.None);

        Assert.Equal([ChatTexts.Failed], _notifier.Replies);
        Assert.Equal(0, _state.Saves);
    }

    [Fact]
    public async Task A_send_failure_is_swallowed_and_the_reply_is_not_repeated()
    {
        await Started();
        _notifier = new RecordingNotifier { FailOnCall = 1 };
        var agent = new FakeChatAgent(_ => "відповідь");
        var processor = Create(agent);

        await processor.HandleAsync(Request(10), CancellationToken.None);
        await processor.HandleAsync(Request(10), CancellationToken.None);

        Assert.Empty(_notifier.Replies);
        Assert.Equal(1, agent.Calls);
        Assert.Equal(1, _state.Saves);
    }

    [Fact]
    public async Task A_long_reply_is_sent_in_parts_in_order()
    {
        await Started();
        var agent = new FakeChatAgent(_ => new string('а', 5000) + "\n\n" + new string('б', 5000));

        await Create(agent).HandleAsync(Request(10), CancellationToken.None);

        Assert.True(_notifier.Replies.Count >= 3);
        Assert.StartsWith("а", _notifier.Replies[0]);
        Assert.EndsWith("б", _notifier.Replies[^1]);
        Assert.All(_notifier.Replies, part => Assert.True(part.Length <= 4096));
    }

    [Fact]
    public async Task Cancellation_is_not_swallowed()
    {
        await Started();
        using var cts = new CancellationTokenSource();
        var agent = new FakeChatAgent(async (_, _) =>
        {
            await cts.CancelAsync();
            throw new OperationCanceledException(cts.Token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create(agent).HandleAsync(Request(10), cts.Token));
    }

    [Fact]
    public async Task A_model_timeout_is_a_failure_with_one_message_and_not_a_redelivery()
    {
        await Started();
        // HttpClient and the OpenAI client report their own timeout as TaskCanceledException while our token is not cancelled.
        var agent = new FakeChatAgent((_, _) => throw new TaskCanceledException("timed out"));

        await Create(agent).HandleAsync(Request(10), CancellationToken.None);

        Assert.Equal([ChatTexts.Failed], _notifier.Replies);
        Assert.Equal(0, _state.Saves);
    }

    [Fact]
    public async Task A_telegram_timeout_while_sending_is_swallowed_and_not_repeated()
    {
        await Started();
        var notifier = new TimingOutNotifier();
        var agent = new FakeChatAgent(_ => "відповідь");
        var processor = new ChatProcessor(_state, agent, new ChatDelivery(notifier, new FakeTimeProvider(Now), NullLogger<ChatDelivery>.Instance), new PostAgent.Tests.SettingsMenu.MenuHarness().ProposalSender, NullLogger<ChatProcessor>.Instance);

        await processor.HandleAsync(Request(10), CancellationToken.None);

        Assert.Equal(1, notifier.Attempts);
        Assert.Equal(1, agent.Calls);
    }

    private sealed class TimingOutNotifier : PostAgent.Shared.Notifications.ITelegramNotifier
    {
        public int Attempts { get; private set; }

        public Task SendAsync(string html, CancellationToken ct)
        {
            Attempts++;
            throw new TaskCanceledException("timed out");
        }

        public Task SendAsync(string html, PostAgent.Shared.Notifications.InlineKeyboard keyboard, CancellationToken ct) => SendAsync(html, ct);

        public Task SendToAsync(long chatId, string html, CancellationToken ct) => SendAsync(html, ct);

        public Task SendToAsync(long chatId, string html, PostAgent.Shared.Notifications.ReplyKeyboard keyboard, CancellationToken ct) => SendAsync(html, ct);
    }

    [Fact]
    public async Task Answers_in_the_chat_that_asked_and_leaves_the_conversation_of_the_other_chat_alone()
    {
        await _state.StartNewAsync(42, Report(), CancellationToken.None);
        await _state.StartNewAsync(43, Report(), CancellationToken.None);

        await Create(new FakeChatAgent(text => "ans:" + text)).HandleAsync(Request(10, "питання", chatId: 43), CancellationToken.None);

        Assert.Equal([(43L, "ans:питання")], _notifier.SentTo);
        Assert.Equal([10L], _state.Of(43)?.ProcessedUpdateIds);
        Assert.Empty(_state.Of(42)!.ProcessedUpdateIds);
    }

    [Fact]
    public async Task A_question_from_a_chat_without_a_conversation_is_dropped_even_when_another_chat_has_one()
    {
        await _state.StartNewAsync(42, Report(), CancellationToken.None);

        await Create(new FakeChatAgent(text => "ans")).HandleAsync(Request(10, chatId: 43), CancellationToken.None);

        Assert.Empty(_notifier.SentTo);
    }
}
