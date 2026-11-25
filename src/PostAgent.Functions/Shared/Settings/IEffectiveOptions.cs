namespace PostAgent.Shared.Settings;

// The App Setting options with what the owner set from the bot on top. Jobs take one snapshot per run and pass it down.
public interface IEffectiveOptions
{
    // Cached for a short time, so a busy function does not read the blob on every call.
    Task<AgentOptions> GetAsync(CancellationToken ct);

    // Skips the cache: the menu shows what was just saved.
    Task<AgentOptions> GetFreshAsync(CancellationToken ct);

    // Drops this instance's cache after the editor saved a change.
    void Invalidate();
}
