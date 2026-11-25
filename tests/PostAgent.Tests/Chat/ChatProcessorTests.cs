using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Chat;
using PostAgent.Shared.Chat;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Chat;

public class ChatProcessorTests
{
    private readonly InMemoryChatStateStore _state = new();
    private RecordingNotifier _notifier = new();

    private static ReportSnapshot Report(string id = "r1") => new(id, Now, "Огляд", [], []);

    private static ChatRequest Request(long updateId, string text = "питання", string reportId = "r1") => new(updateId, text, reportId);

    private ChatProcessor Create(FakeChatAgent agent) =>
        new(_state, agent, _notifier, new FakeTimeProvider(Now), NullLogger<ChatProcessor>.Instance);

    private async Task Started(string id = "r1") => await _state.StartNewAsync(Report(id), CancellationToken.None);

    [Fact]
    public async Task Replies_with_the_answer_and_remembers_the_update()
    {
        await Started();
        var agent = new FakeChatAgent(text => "відповідь на " + text);

        await Create(agent).HandleAsync(Request(10), CancellationToken.None);

        Assert.Equal(["відповідь на питання"], _notifier.Sent);
        Assert.Equal([10L], _state.Current?.ProcessedUpdateIds);
        Assert.NotNull(_state.Current?.Session);
    }

    [Fact]
    public async Task A_redelivered_update_is_answered_once()
    {
        await Started();
        var agent = new FakeChatAgent(_ => "відповідь");
        var processor = Create(agent);

        await processor.HandleAsync(Request(10), CancellationToken.None);
        await processor.HandleAsync(Request(10), CancellationToken.None);

        Assert.Single(_notifier.Sent);
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

        Assert.Equal(["ans:B", "ans:A"], _notifier.Sent);
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

        Assert.Equal(["ans"], _notifier.Sent);
    }

    [Fact]
    public async Task A_new_digest_during_the_turn_drops_the_reply_and_keeps_the_new_conversation()
    {
        await Started();
        var agent = new FakeChatAgent(async (_, _) =>
        {
            await _state.StartNewAsync(Report("r2"), CancellationToken.None);
            return new ChatReply("стара відповідь", FakeChatAgent.Session());
        });

        await Create(agent).HandleAsync(Request(10), CancellationToken.None);

        Assert.Empty(_notifier.Sent);
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

        Assert.Empty(_notifier.Sent);
        Assert.Null(_state.Current);
    }

    [Fact]
    public async Task A_message_queued_before_the_digest_is_dropped_without_calling_the_model()
    {
        await Started("r2");
        var agent = new FakeChatAgent(_ => "відповідь");

        await Create(agent).HandleAsync(Request(10, reportId: "r1"), CancellationToken.None);

        Assert.Equal(0, agent.Calls);
        Assert.Empty(_notifier.Sent);
    }

    [Fact]
    public async Task With_no_conversation_the_message_is_dropped()
    {
        var agent = new FakeChatAgent(_ => "відповідь");

        await Create(agent).HandleAsync(Request(10), CancellationToken.None);

        Assert.Equal(0, agent.Calls);
        Assert.Empty(_notifier.Sent);
    }

    [Fact]
    public async Task Gives_up_after_three_attempts_when_the_state_keeps_changing_and_sends_nothing()
    {
        await Started();
        var agent = new FakeChatAgent(async (_, _) =>
        {
            await _state.StartNewAsync(Report(), CancellationToken.None); // same report, new version
            return new ChatReply("відповідь", FakeChatAgent.Session());
        });

        await Create(agent).HandleAsync(Request(10), CancellationToken.None);

        Assert.Equal(3, agent.Calls);
        Assert.Empty(_notifier.Sent);
    }

    [Fact]
    public async Task A_model_failure_sends_one_short_message_and_saves_nothing()
    {
        await Started();
        var agent = new FakeChatAgent((_, _) => throw new InvalidOperationException("model is down"));

        await Create(agent).HandleAsync(Request(10), CancellationToken.None);

        Assert.Equal([ChatTexts.Failed], _notifier.Sent);
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

        Assert.Empty(_notifier.Sent);
        Assert.Equal(1, agent.Calls);
        Assert.Equal(1, _state.Saves);
    }

    [Fact]
    public async Task A_long_reply_is_sent_in_parts_in_order()
    {
        await Started();
        var agent = new FakeChatAgent(_ => new string('а', 5000) + "\n\n" + new string('б', 5000));

        await Create(agent).HandleAsync(Request(10), CancellationToken.None);

        Assert.True(_notifier.Sent.Count >= 3);
        Assert.StartsWith("а", _notifier.Sent[0]);
        Assert.EndsWith("б", _notifier.Sent[^1]);
        Assert.All(_notifier.Sent, part => Assert.True(part.Length <= 4096));
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

        Assert.Equal([ChatTexts.Failed], _notifier.Sent);
        Assert.Equal(0, _state.Saves);
    }

    [Fact]
    public async Task A_telegram_timeout_while_sending_is_swallowed_and_not_repeated()
    {
        await Started();
        var notifier = new TimingOutNotifier();
        var agent = new FakeChatAgent(_ => "відповідь");
        var processor = new ChatProcessor(_state, agent, notifier, new FakeTimeProvider(Now), NullLogger<ChatProcessor>.Instance);

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
    }
}
