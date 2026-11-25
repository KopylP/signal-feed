using PostAgent.Shared.Settings;

namespace PostAgent.Tests.Fakes;

// Same contract as the blob store: a version number plays the ETag; a null handle means "create only if absent".
public sealed class InMemoryOverridesStore : IOverridesStore
{
    private int _version;

    // Runs inside TrySaveAsync before the version check, to simulate another writer winning the race.
    public Action? BeforeSave { get; set; }

    public SettingsOverrides? Current { get; private set; }

    public int Saves { get; private set; }

    public void Seed(SettingsOverrides overrides)
    {
        Current = overrides;
        _version++;
    }

    public Task<OverridesHandle?> LoadAsync(CancellationToken ct) =>
        Task.FromResult(Current is null ? null : new OverridesHandle(Current, _version.ToString()));

    public Task<bool> TrySaveAsync(OverridesHandle? current, SettingsOverrides updated, CancellationToken ct)
    {
        BeforeSave?.Invoke();
        var matches = current is null ? Current is null : Current is not null && current.ETag == _version.ToString();
        if (!matches)
        {
            return Task.FromResult(false);
        }

        Current = updated;
        _version++;
        Saves++;
        return Task.FromResult(true);
    }
}
