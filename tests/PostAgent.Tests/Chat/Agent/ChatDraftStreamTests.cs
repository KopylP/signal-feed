using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PostAgent.Chat.Agent;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Chat.Agent;

public class ChatDraftStreamTests
{
    private readonly RecordingDrafts _drafts = new();
    private readonly FakeTimeProvider _time = new(Now);

    private ChatDraftStream Create() => new(_drafts, _time, NullLogger<ChatDraftStream>.Instance, ChatId, 7);

    [Fact]
    public async Task Starting_sends_an_empty_draft_that_shows_thinking()
    {
        await Create().StartAsync(CancellationToken.None);

        Assert.Equal([(ChatId, 7L, string.Empty)], _drafts.Sent);
    }

    [Fact]
    public async Task The_text_so_far_is_sent_under_the_same_draft_id()
    {
        var stream = Create();
        await stream.StartAsync(CancellationToken.None);

        _time.Advance(ChatDraftStream.MinInterval);
        await stream.OnTextAsync("Перша частина", CancellationToken.None);
        _time.Advance(ChatDraftStream.MinInterval);
        await stream.OnTextAsync("Перша частина і друга", CancellationToken.None);

        Assert.Equal(["", "Перша частина", "Перша частина і друга"], _drafts.Sent.Select(s => s.Text));
        Assert.All(_drafts.Sent, s => Assert.Equal((ChatId, 7L), (s.ChatId, s.DraftId)));
    }

    [Fact]
    public async Task Updates_that_come_too_fast_are_skipped_and_the_next_one_carries_everything()
    {
        var stream = Create();
        await stream.StartAsync(CancellationToken.None);

        await stream.OnTextAsync("a", CancellationToken.None);
        _time.Advance(TimeSpan.FromMilliseconds(100));
        await stream.OnTextAsync("a b", CancellationToken.None);
        _time.Advance(ChatDraftStream.MinInterval);
        await stream.OnTextAsync("a b c", CancellationToken.None);

        Assert.Equal(["", "a b c"], _drafts.Sent.Select(s => s.Text));
    }

    [Fact]
    public async Task The_same_text_and_an_empty_text_are_not_sent_again()
    {
        var stream = Create();
        _time.Advance(ChatDraftStream.MinInterval);
        await stream.OnTextAsync("a", CancellationToken.None);
        _time.Advance(ChatDraftStream.MinInterval);

        await stream.OnTextAsync("a", CancellationToken.None);
        await stream.OnTextAsync(string.Empty, CancellationToken.None);

        Assert.Single(_drafts.Sent);
    }

    [Fact]
    public async Task A_failed_send_ends_the_stream_without_throwing()
    {
        var stream = Create();
        _drafts.Fail = true;

        await stream.StartAsync(CancellationToken.None);
        _drafts.Fail = false;
        _time.Advance(ChatDraftStream.MinInterval);
        await stream.OnTextAsync("a", CancellationToken.None);

        Assert.Empty(_drafts.Sent);
    }

    [Fact]
    public async Task A_cancelled_send_is_not_swallowed()
    {
        var stream = new ChatDraftStream(new CancellingDrafts(), _time, NullLogger<ChatDraftStream>.Instance, ChatId, 7);

        await Assert.ThrowsAsync<OperationCanceledException>(() => stream.StartAsync(CancellationToken.None));
    }

    private sealed class CancellingDrafts : IChatDrafts
    {
        public Task SendAsync(long chatId, long draftId, string text, CancellationToken ct) => throw new OperationCanceledException();
    }
}
