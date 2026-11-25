using PostAgent.Shared.Chat;

namespace PostAgent.Tests.Fakes;

// Same contract as the blob store: a version number plays the ETag, StartNew overwrites without a condition. One state per chat.
public sealed class InMemoryChatStateStore : IChatStateStore
{
    private readonly Dictionary<long, (ChatState State, int Version)> _states = [];
    private int _version;

    // Runs inside TrySaveAsync before the version check, to simulate another writer winning the race.
    public Action? BeforeSave { get; set; }

    public int Saves { get; private set; }

    // The state of the chat that most tests use.
    public ChatState? Current => Of(TestData.ChatId);

    public ChatState? Of(long chatId) => _states.TryGetValue(chatId, out var entry) ? entry.State : null;

    public Task<ChatStateHandle?> LoadAsync(long chatId, CancellationToken ct) =>
        Task.FromResult(_states.TryGetValue(chatId, out var entry) ? new ChatStateHandle(entry.State, entry.Version.ToString()) : null);

    public Task StartNewAsync(long chatId, ReportSnapshot report, CancellationToken ct)
    {
        _states[chatId] = (new ChatState(report, [], null), ++_version);
        return Task.CompletedTask;
    }

    public Task<bool> TrySaveAsync(long chatId, ChatStateHandle current, ChatState updated, CancellationToken ct)
    {
        BeforeSave?.Invoke();
        if (!_states.TryGetValue(chatId, out var entry) || current.ETag != entry.Version.ToString())
        {
            return Task.FromResult(false);
        }

        _states[chatId] = (updated, ++_version);
        Saves++;
        return Task.FromResult(true);
    }

    public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct)
    {
        var stale = _states.Where(e => e.Value.State.Report.CreatedAt < cutoff).Select(e => e.Key).ToList();
        foreach (var chatId in stale)
        {
            _states.Remove(chatId);
        }

        return Task.FromResult(stale.Count);
    }

    public void Clear() => _states.Clear();
}
