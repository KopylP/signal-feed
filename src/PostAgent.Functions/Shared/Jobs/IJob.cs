namespace PostAgent.Shared.Jobs;

// A unit of scheduled work. The timer function and the run-now queue function both call it,
// so the logic lives in one place and a trigger stays a thin wrapper.
public interface IJob
{
    // Lower-case name used in a run-now message, for example "digest".
    string Name { get; }

    Task RunAsync(CancellationToken ct);
}
