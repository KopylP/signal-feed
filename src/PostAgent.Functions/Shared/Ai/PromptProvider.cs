using PostAgent.Shared.Settings;

namespace PostAgent.Shared.Ai;

// Reads the overrides blob for the edits, with a short cache: the scorer asks once per queue message.
// A blob that cannot be read or parsed means the shipped prompt, so a storage error never stops a model call.
public sealed class PromptProvider(PromptLibrary library, IOverridesStore store, TimeProvider time, ILogger<PromptProvider> log) : IPromptProvider
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CacheFailureFor = TimeSpan.FromSeconds(5);

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

        IReadOnlyDictionary<string, string>? value = null;
        var keepFor = CacheFor;
        try
        {
            value = (await store.LoadAsync(ct))?.Value.PromptSections;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "The prompt edits could not be read, using the shipped prompts");
            keepFor = CacheFailureFor;
        }

        lock (_gate)
        {
            _cached = (value, time.GetUtcNow() + keepFor);
        }

        return value;
    }
}
