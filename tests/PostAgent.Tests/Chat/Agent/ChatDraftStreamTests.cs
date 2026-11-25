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
    public async Task Starting_sends_the_thinking_placeholder()
    {
        await Create().StartAsync(CancellationToken.None);

        Assert.Equal([(ChatId, 7L, ChatDraftStream.Placeholder)], _drafts.Sent);
    }

    [Fact]
    public async Task The_text_so_far_is_sent_under_the_same_draft_id()
    {
        var stream = Create();
        await stream.StartAsync(CancellationToken.None);

        _time.Advance(ChatDraftStream.MinInterval);
        await stream.OnTextAsync("Перша частина тексту", CancellationToken.None);
        _time.Advance(ChatDraftStream.MinInterval);
        await stream.OnTextAsync("Перша частина тексту і друга", CancellationToken.None);

        Assert.Equal([ChatDraftStream.Placeholder, "Перша частина тексту", "Перша частина тексту і друга"], _drafts.Sent.Select(s => s.Text));
        Assert.All(_drafts.Sent, s => Assert.Equal((ChatId, 7L), (s.ChatId, s.DraftId)));
    }

    [Fact]
    public async Task Updates_that_come_too_fast_are_skipped_and_the_next_one_carries_everything()
    {
        var stream = Create();
        await stream.StartAsync(CancellationToken.None);

        await stream.OnTextAsync("a b c", CancellationToken.None);
        _time.Advance(TimeSpan.FromMilliseconds(100));
        await stream.OnTextAsync("a b c d", CancellationToken.None);
        _time.Advance(ChatDraftStream.MinInterval);
        await stream.OnTextAsync("a b c d e", CancellationToken.None);

        Assert.Equal([ChatDraftStream.Placeholder, "a b c d e"], _drafts.Sent.Select(s => s.Text));
    }

    [Fact]
    public async Task One_or_two_words_are_not_shown_and_the_placeholder_stays()
    {
        var stream = Create();
        await stream.StartAsync(CancellationToken.None);
        _time.Advance(ChatDraftStream.MinInterval);

        await stream.OnTextAsync("Так", CancellationToken.None);
        _time.Advance(ChatDraftStream.MinInterval);
        await stream.OnTextAsync("Так, це", CancellationToken.None);

        Assert.Equal([ChatDraftStream.Placeholder], _drafts.Sent.Select(s => s.Text));
    }

    [Fact]
    public async Task The_third_word_replaces_the_placeholder_with_the_text()
    {
        var stream = Create();
        await stream.StartAsync(CancellationToken.None);
        _time.Advance(ChatDraftStream.MinInterval);

        await stream.OnTextAsync("Так, це ось", CancellationToken.None);

        Assert.Equal([ChatDraftStream.Placeholder, "Так, це ось"], _drafts.Sent.Select(s => s.Text));
    }

    [Fact]
    public async Task The_same_text_and_an_empty_text_are_not_sent_again()
    {
        var stream = Create();
        _time.Advance(ChatDraftStream.MinInterval);
        await stream.OnTextAsync("a b c", CancellationToken.None);
        _time.Advance(ChatDraftStream.MinInterval);

        await stream.OnTextAsync("a b c", CancellationToken.None);
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
        await stream.OnTextAsync("a b c", CancellationToken.None);

        Assert.Empty(_drafts.Sent);
    }

    [Fact]
    public async Task A_timeout_reported_as_a_cancellation_ends_the_preview_but_not_the_answer()
    {
        _drafts.CancelSends = true;
        var stream = Create();

        await stream.StartAsync(CancellationToken.None);
        _drafts.CancelSends = false;
        _time.Advance(ChatDraftStream.MinInterval);
        await stream.OnTextAsync("a b c", CancellationToken.None);

        Assert.Empty(_drafts.Sent);
    }

    [Fact]
    public async Task When_the_host_is_stopping_the_cancellation_is_not_swallowed()
    {
        _drafts.CancelSends = true;
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create().StartAsync(cts.Token));
    }

    [Fact]
    public async Task The_interval_restarts_after_every_send()
    {
        var stream = Create();
        await stream.StartAsync(CancellationToken.None);

        _time.Advance(ChatDraftStream.MinInterval);
        await stream.OnTextAsync("a b c", CancellationToken.None);
        _time.Advance(TimeSpan.FromMilliseconds(100));
        await stream.OnTextAsync("a b c d", CancellationToken.None);
        _time.Advance(ChatDraftStream.MinInterval);
        await stream.OnTextAsync("a b c d e", CancellationToken.None);

        Assert.Equal([ChatDraftStream.Placeholder, "a b c", "a b c d e"], _drafts.Sent.Select(s => s.Text));
    }

    [Fact]
    public async Task A_text_over_the_limit_shows_its_end()
    {
        var stream = Create();
        await stream.StartAsync(CancellationToken.None);
        _time.Advance(ChatDraftStream.MinInterval);

        await stream.OnTextAsync("a b c " + new string('a', 100) + new string('z', ChatDraftStream.MaxChars), CancellationToken.None);

        var sent = _drafts.Sent[^1].Text;
        Assert.Equal(ChatDraftStream.MaxChars, sent.Length);
        Assert.Equal(new string('z', ChatDraftStream.MaxChars), sent);
    }

    [Fact]
    public async Task While_nothing_is_written_the_last_text_is_sent_again_to_keep_the_preview_alive()
    {
        var stream = Create();
        await stream.StartAsync(CancellationToken.None);
        using var stop = new CancellationTokenSource();
        var keepAlive = stream.RunKeepAliveAsync(stop.Token);

        _time.Advance(ChatDraftStream.KeepAliveEvery);
        await WaitForAsync(() => _drafts.Sent.Count >= 2);
        _time.Advance(ChatDraftStream.KeepAliveEvery);
        await WaitForAsync(() => _drafts.Sent.Count >= 3);
        await stop.CancelAsync();
        await keepAlive;

        Assert.Equal(3, _drafts.Sent.Count);
        Assert.All(_drafts.Sent, s => Assert.Equal((ChatId, 7L, ChatDraftStream.Placeholder), (s.ChatId, s.DraftId, s.Text)));
    }

    [Fact]
    public async Task The_keep_alive_ends_quietly_when_the_answer_is_done()
    {
        var stream = Create();
        await stream.StartAsync(CancellationToken.None);
        using var stop = new CancellationTokenSource();
        var keepAlive = stream.RunKeepAliveAsync(stop.Token);

        await stop.CancelAsync();
        await keepAlive;

        Assert.Single(_drafts.Sent);
    }

    [Fact]
    public async Task A_text_sent_before_a_long_pause_is_sent_again_well_before_the_draft_expires()
    {
        var stream = Create();
        await stream.StartAsync(CancellationToken.None);
        _time.Advance(ChatDraftStream.MinInterval);
        await stream.OnTextAsync("Зараз гляну в пости", CancellationToken.None);
        using var stop = new CancellationTokenSource();
        var keepAlive = stream.RunKeepAliveAsync(stop.Token);
        var sentAt = _time.GetUtcNow();

        DateTimeOffset? resentAt = null;
        for (var step = 0; step < 6 && resentAt is null; step++)
        {
            _time.Advance(ChatDraftStream.KeepAliveCheckEvery);
            await Task.Delay(30);
            if (_drafts.Sent.Count >= 3)
            {
                resentAt = _time.GetUtcNow();
            }
        }

        await stop.CancelAsync();
        await keepAlive;

        Assert.NotNull(resentAt);
        Assert.True(resentAt - sentAt < TimeSpan.FromSeconds(30), "the draft must be sent again before it expires");
        Assert.Equal("Зараз гляну в пости", _drafts.Sent[^1].Text);
    }

    [Fact]
    public async Task A_text_that_waited_in_the_spacing_window_is_sent_when_nothing_else_arrives()
    {
        var stream = Create();
        await stream.StartAsync(CancellationToken.None);
        _time.Advance(ChatDraftStream.MinInterval);
        await stream.OnTextAsync("Зараз гляну в", CancellationToken.None);
        _time.Advance(TimeSpan.FromMilliseconds(50));
        await stream.OnTextAsync("Зараз гляну в пости.", CancellationToken.None);
        using var stop = new CancellationTokenSource();
        var keepAlive = stream.RunKeepAliveAsync(stop.Token);

        _time.Advance(ChatDraftStream.KeepAliveCheckEvery);
        await WaitForAsync(() => _drafts.Sent.Count >= 3);
        await stop.CancelAsync();
        await keepAlive;

        Assert.Equal([ChatDraftStream.Placeholder, "Зараз гляну в", "Зараз гляну в пости."], _drafts.Sent.Select(s => s.Text));
    }

    // The keep-alive runs on its own task, so the test waits for what it does.
    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }
}
