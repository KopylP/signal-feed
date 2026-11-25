using PostAgent.Shared.Settings;

namespace PostAgent.Tests.Fakes;

// Reads counts how often a job asks, so a test can check that a run takes one snapshot.
internal sealed class FakeEffectiveOptions(AgentOptions current) : IEffectiveOptions
{
    public AgentOptions Current { get; set; } = current;

    public int Reads { get; private set; }

    public Task<AgentOptions> GetAsync(CancellationToken ct)
    {
        Reads++;
        return Task.FromResult(Current);
    }

    public Task<AgentOptions> GetFreshAsync(CancellationToken ct) => GetAsync(ct);

    public void Invalidate()
    {
    }
}
