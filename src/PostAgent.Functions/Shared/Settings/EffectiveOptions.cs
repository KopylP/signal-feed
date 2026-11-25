using Microsoft.Extensions.Options;

namespace PostAgent.Shared.Settings;

public sealed class EffectiveOptions(
    IOverridesStore store,
    IOptions<AgentOptions> app,
    TimeProvider time,
    ILogger<EffectiveOptions> log) : IEffectiveOptions
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);

    private readonly Lock _gate = new();
    private (AgentOptions Value, DateTimeOffset Expires)? _cached;

    public async Task<AgentOptions> GetAsync(CancellationToken ct)
    {
        lock (_gate)
        {
            if (_cached is { } cached && time.GetUtcNow() < cached.Expires)
            {
                return cached.Value;
            }
        }

        return await GetFreshAsync(ct);
    }

    public async Task<AgentOptions> GetFreshAsync(CancellationToken ct)
    {
        var value = Resolve(await store.LoadAsync(ct));
        lock (_gate)
        {
            _cached = (value, time.GetUtcNow() + CacheFor);
        }

        return value;
    }

    public void Invalidate()
    {
        lock (_gate)
        {
            _cached = null;
        }
    }

    // A value that stops a job must never reach it: when the combination is invalid the App Settings win and the menu says so.
    private AgentOptions Resolve(OverridesHandle? handle)
    {
        if (handle is null)
        {
            return app.Value;
        }

        var candidate = OverridesApplier.Apply(app.Value, handle.Value);
        var errors = OverridesApplier.Check(candidate);
        if (errors.Count == 0)
        {
            return candidate;
        }

        log.LogWarning("Settings overrides are ignored: {Errors}", string.Join("; ", errors));
        return app.Value;
    }
}
