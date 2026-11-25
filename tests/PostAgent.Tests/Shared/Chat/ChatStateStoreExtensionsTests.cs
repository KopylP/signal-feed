using PostAgent.Shared.Chat;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Shared.Chat;

public class ChatStateStoreExtensionsTests
{
    private readonly InMemoryChatStateStore _store = new();

    private async Task SeedWithSessionAsync()
    {
        await _store.StartNewAsync(ChatId, new ReportSnapshot("r1", Now, "Огляд", [], []), CancellationToken.None);
        var handle = (await _store.LoadAsync(ChatId, CancellationToken.None))!;
        await _store.TrySaveAsync(ChatId, handle, handle.State.WithProcessed(5, FakeChatAgent.Session()), CancellationToken.None);
    }

    // Another turn writes (and keeps its session) before every save of the test, so the clear never wins.
    private void AnotherTurnAlwaysWinsFor(InMemoryChatStateStore store, long chatId)
    {
        var n = 100;
        Action? hook = null;
        hook = () =>
        {
            store.BeforeSave = null;
            var handle = store.LoadAsync(chatId, CancellationToken.None).GetAwaiter().GetResult()!;
            store.TrySaveAsync(chatId, handle, handle.State.WithProcessed(n++, FakeChatAgent.Session()), CancellationToken.None).GetAwaiter().GetResult();
            store.BeforeSave = hook;
        };
        store.BeforeSave = hook;
    }

    [Fact]
    public async Task Clearing_drops_the_session_and_keeps_the_digest_and_the_handled_ids()
    {
        await SeedWithSessionAsync();

        Assert.True(await _store.ClearConversationAsync(ChatId, CancellationToken.None));

        var state = _store.Current!;
        Assert.Null(state.Session);
        Assert.Equal("r1", state.Report.ReportId);
        Assert.Equal([5L], state.ProcessedUpdateIds);
    }

    [Fact]
    public async Task Nothing_to_clear_is_not_an_error_and_writes_nothing()
    {
        Assert.True(await _store.ClearConversationAsync(ChatId, CancellationToken.None));
        await _store.StartNewAsync(ChatId, new ReportSnapshot("r1", Now, null, [], []), CancellationToken.None);
        var saves = _store.Saves;

        Assert.True(await _store.ClearConversationAsync(ChatId, CancellationToken.None));

        Assert.Equal(saves, _store.Saves);
    }

    [Fact]
    public async Task One_lost_write_race_is_retried()
    {
        await SeedWithSessionAsync();
        var raced = false;
        _store.BeforeSave = () =>
        {
            if (raced)
            {
                return;
            }

            raced = true;
            var handle = _store.LoadAsync(ChatId, CancellationToken.None).GetAwaiter().GetResult()!;
            _store.TrySaveAsync(ChatId, handle, handle.State.WithProcessed(6, FakeChatAgent.Session()), CancellationToken.None).GetAwaiter().GetResult();
        };

        Assert.True(await _store.ClearConversationAsync(ChatId, CancellationToken.None));
        Assert.Null(_store.Current!.Session);
        Assert.Contains(6L, _store.Current.ProcessedUpdateIds);
    }

    [Fact]
    public async Task A_state_that_keeps_changing_is_reported_as_not_cleared()
    {
        await SeedWithSessionAsync();
        AnotherTurnAlwaysWinsFor(_store, ChatId);

        Assert.False(await _store.ClearConversationAsync(ChatId, CancellationToken.None));
    }

    [Fact]
    public async Task A_conversation_can_start_before_any_digest_and_a_digest_is_never_replaced_by_it()
    {
        Assert.True(await _store.TryStartAsync(ChatId, ReportSnapshot.None(Now), CancellationToken.None));
        Assert.False(_store.Current!.Report.HasDigest);

        await _store.StartNewAsync(ChatId, new ReportSnapshot("r1", Now, null, [], []), CancellationToken.None);

        Assert.False(await _store.TryStartAsync(ChatId, ReportSnapshot.None(Now), CancellationToken.None));
        Assert.Equal("r1", _store.Current!.Report.ReportId);
    }

    [Fact]
    public void A_conversation_without_a_digest_survives_the_blob_format()
    {
        var written = ChatStateJson.Write(new ChatState(ReportSnapshot.None(Now), [], null));

        Assert.DoesNotContain("hasDigest", written, StringComparison.OrdinalIgnoreCase);
        Assert.True(ChatStateJson.TryRead(written, out var read));
        Assert.False(read.Report.HasDigest);
    }
}
