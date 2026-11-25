using PostAgent.Shared.Settings;

namespace PostAgent.Tests.Fakes;

// Same contract as the blob store, one blob per chat: a version number plays the ETag; a null handle means "create only if absent".
public sealed class InMemoryProfileStore : IProfileStore
{
    private readonly Dictionary<long, (string Text, int Version)> _blobs = [];

    // Makes the next save lose the race, like another writer that got there first.
    public bool FailNextSave { get; set; }

    // Makes the next save throw, like a storage error that is not a lost race.
    public bool ThrowOnSave { get; set; }

    // The chat that most tests talk to.
    public string? Text => TextOf(TestData.ChatId);

    public string? TextOf(long chatId) => _blobs.TryGetValue(chatId, out var blob) ? blob.Text : null;

    public void Seed(string text) => Seed(TestData.ChatId, text);

    public void Seed(long chatId, string text) => _blobs[chatId] = (text, (_blobs.TryGetValue(chatId, out var old) ? old.Version : 0) + 1);

    public Task<ProfileHandle?> LoadAsync(long chatId, CancellationToken ct) =>
        Task.FromResult(_blobs.TryGetValue(chatId, out var blob) ? new ProfileHandle(blob.Text, blob.Version.ToString()) : null);

    public Task<bool> TrySaveAsync(long chatId, ProfileHandle? current, string text, CancellationToken ct)
    {
        if (ThrowOnSave)
        {
            throw new IOException("storage down");
        }

        var exists = _blobs.TryGetValue(chatId, out var blob);
        if (FailNextSave || (current is null ? exists : !exists || current.ETag != blob.Version.ToString()))
        {
            FailNextSave = false;
            return Task.FromResult(false);
        }

        Seed(chatId, text);
        return Task.FromResult(true);
    }
}
