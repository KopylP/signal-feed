using PostAgent.Shared.Jobs;

namespace PostAgent.Tests.Fakes;

internal sealed class FakeJob(string name, Func<Task>? run = null) : IJob
{
    public string Name => name;
    public int Runs { get; private set; }

    public Task RunAsync(CancellationToken ct)
    {
        Runs++;
        return run?.Invoke() ?? Task.CompletedTask;
    }
}
