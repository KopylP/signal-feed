using PostAgent.Shared.Settings;

namespace PostAgent.Shared.Ai;

// Reads the overrides blob for the edits, with a short cache: the scorer asks once per queue message. A blob that cannot be read means the shipped prompt.
public sealed class PromptProvider(PromptLibrary library, IOverridesStore store, TimeProvider time) : IPromptProvider
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);

    private readonly Lock _gate = new();
    private (IReadOnlyDictionary<string, string>? Value, DateTimeOffset Expires)? _cached;

    public async Task<string> GetAsync(string file, CancellationToken ct)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        return library.Render(name, await EditsAsync(ct));
    }

    private async Task<IReadOnlyDictionary<string, string>?> EditsAsync(CancellationToken ct)
    {
        lock (_gate)
        {
            if (_cached is { } cached && time.GetUtcNow() < cached.Expires)
            {
                return cached.Value;
            }
        }

        var value = (await store.LoadAsync(ct))?.Value.PromptSections;
        lock (_gate)
        {
            _cached = (value, time.GetUtcNow() + CacheFor);
        }

        return value;
    }
}
