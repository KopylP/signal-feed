using PostAgent.Shared.Settings;

namespace PostAgent.Tests.Fakes;

// Same contract as the blob store: a version number plays the ETag; a null handle means "create only if absent".
public sealed class InMemoryProfileStore : IProfileStore
{
    private int _version;

    public string? Text { get; private set; }

    // Makes the next save lose the race, like another writer that got there first.
    public bool FailNextSave { get; set; }

    public void Seed(string text)
    {
        Text = text;
        _version++;
    }

    public Task<ProfileHandle?> LoadAsync(CancellationToken ct) =>
        Task.FromResult(Text is null ? null : new ProfileHandle(Text, _version.ToString()));

    public Task<bool> TrySaveAsync(ProfileHandle? current, string text, CancellationToken ct)
    {
        if (FailNextSave || (current is null ? Text is not null : current.ETag != _version.ToString()))
        {
            FailNextSave = false;
            return Task.FromResult(false);
        }

        Text = text;
        _version++;
        return Task.FromResult(true);
    }
}
