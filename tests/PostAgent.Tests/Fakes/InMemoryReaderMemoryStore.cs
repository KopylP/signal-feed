using PostAgent.Chat.Memory;

namespace PostAgent.Tests.Fakes;

// Same contract as the blob store, one file per note and chat: a version number plays the ETag.
public sealed class InMemoryReaderMemoryStore : IReaderMemoryStore
{
    private readonly Dictionary<long, SortedDictionary<string, (string Text, int Version)>> _notes = [];
    private readonly Dictionary<long, (string Text, DateTimeOffset LastModified, int Version)> _legacy = [];
    private int _version;

    // Makes the next write or delete lose the race, like another writer that got there first.
    public bool FailNextWrite { get; set; }

    // Makes reading the legacy profile throw, like a storage error.
    public bool ThrowOnLegacyProfile { get; set; }

    public IReadOnlyList<string> FileNames(long chatId = TestData.ChatId) => Notes(chatId).Keys.ToList();

    public string? TextOf(string fileName, long chatId = TestData.ChatId) => Notes(chatId).TryGetValue(fileName, out var note) ? note.Text : null;

    public bool HasLegacyProfile(long chatId = TestData.ChatId) => _legacy.ContainsKey(chatId);

    public void Seed(string fileName, string text, long chatId = TestData.ChatId) => Notes(chatId)[fileName] = (text, ++_version);

    public void SeedLegacyProfile(string text, DateTimeOffset lastModified, long chatId = TestData.ChatId) => _legacy[chatId] = (text, lastModified, ++_version);

    public Task<IReadOnlyList<StoredMemoryNote>> ListAsync(long chatId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StoredMemoryNote>>(
            Notes(chatId).Select(n => new StoredMemoryNote(new MemoryNote(n.Key, n.Value.Text), Version(n.Value.Version))).ToList());

    public Task<bool> TryCreateAsync(long chatId, MemoryNote note, CancellationToken ct)
    {
        if (LosesRace() || Notes(chatId).ContainsKey(note.FileName))
        {
            return Task.FromResult(false);
        }

        Seed(note.FileName, note.Text, chatId);
        return Task.FromResult(true);
    }

    public Task<bool> TryReplaceAsync(long chatId, StoredMemoryNote current, string text, CancellationToken ct)
    {
        if (LosesRace() || !Matches(chatId, current))
        {
            return Task.FromResult(false);
        }

        Seed(current.Note.FileName, text, chatId);
        return Task.FromResult(true);
    }

    public Task<bool> TryDeleteAsync(long chatId, StoredMemoryNote current, CancellationToken ct) =>
        Task.FromResult(!LosesRace() && Matches(chatId, current) && Notes(chatId).Remove(current.Note.FileName));

    public Task<LegacyProfile?> LoadLegacyProfileAsync(long chatId, CancellationToken ct)
    {
        if (ThrowOnLegacyProfile)
        {
            throw new IOException("storage down");
        }

        return Task.FromResult(_legacy.TryGetValue(chatId, out var p) ? new LegacyProfile(p.Text, p.LastModified, Version(p.Version)) : null);
    }

    public Task<bool> TryDeleteLegacyProfileAsync(long chatId, LegacyProfile profile, CancellationToken ct) =>
        Task.FromResult(_legacy.TryGetValue(chatId, out var p) && Version(p.Version) == profile.ETag && _legacy.Remove(chatId));

    private SortedDictionary<string, (string Text, int Version)> Notes(long chatId) =>
        _notes.TryGetValue(chatId, out var notes) ? notes : _notes[chatId] = new(StringComparer.Ordinal);

    private bool Matches(long chatId, StoredMemoryNote current) =>
        Notes(chatId).TryGetValue(current.Note.FileName, out var note) && Version(note.Version) == current.ETag;

    private bool LosesRace()
    {
        var fail = FailNextWrite;
        FailNextWrite = false;
        return fail;
    }

    private static string Version(int version) => version.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
