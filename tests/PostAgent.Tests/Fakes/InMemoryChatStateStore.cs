using PostAgent.Shared.Chat;

namespace PostAgent.Tests.Fakes;

// Same contract as the blob store: a version number plays the ETag, StartNew overwrites without a condition.
public sealed class InMemoryChatStateStore : IChatStateStore
{
    private ChatState? _state;
    private int _version;

    // Runs inside TrySaveAsync before the version check, to simulate another writer winning the race.
    public Action? BeforeSave { get; set; }

    public int Saves { get; private set; }

    public ChatState? Current => _state;

    public Task<ChatStateHandle?> LoadAsync(CancellationToken ct) =>
        Task.FromResult(_state is null ? null : new ChatStateHandle(_state, _version.ToString()));

    public Task StartNewAsync(ReportSnapshot report, CancellationToken ct)
    {
        _state = new ChatState(report, [], null);
        _version++;
        return Task.CompletedTask;
    }

    public Task<bool> TrySaveAsync(ChatStateHandle current, ChatState updated, CancellationToken ct)
    {
        BeforeSave?.Invoke();
        if (_state is null || current.ETag != _version.ToString())
        {
            return Task.FromResult(false);
        }

        _state = updated;
        _version++;
        Saves++;
        return Task.FromResult(true);
    }

    public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct)
    {
        if (_state is null || _state.Report.CreatedAt >= cutoff)
        {
            return Task.FromResult(0);
        }

        _state = null;
        _version++;
        return Task.FromResult(1);
    }

    public void Clear()
    {
        _state = null;
        _version++;
    }
}
