using Microsoft.Extensions.Logging.Abstractions;
using PostAgent.RunNow;
using PostAgent.Shared.Settings;
using PostAgent.Tests.Fakes;

namespace PostAgent.Tests.RunNow;

public class RunNowFunctionTests
{
    private readonly FakeJob _digest = new("digest");
    private readonly FakeJob _alert = new("alert");

    private RunNowFunction CreateFunction(bool enabled = true, params FakeJob[] extra) =>
        new([_digest, _alert, .. extra],
            Microsoft.Extensions.Options.Options.Create(new AgentOptions { ManualRunsEnabled = enabled }),
            NullLogger<RunNowFunction>.Instance);

    [Theory]
    [InlineData("digest")]
    [InlineData("  Digest\n")]
    [InlineData("DIGEST")]
    public async Task Runs_only_the_job_named_in_the_message(string message)
    {
        await CreateFunction().Run(message, CancellationToken.None);

        Assert.Equal(1, _digest.Runs);
        Assert.Equal(0, _alert.Runs);
    }

    [Fact]
    public async Task Ignores_an_unknown_job_name()
    {
        await CreateFunction().Run("nope", CancellationToken.None);

        Assert.Equal(0, _digest.Runs + _alert.Runs);
    }

    [Fact]
    public async Task Runs_nothing_while_manual_runs_are_disabled()
    {
        await CreateFunction(enabled: false).Run("digest", CancellationToken.None);

        Assert.Equal(0, _digest.Runs);
    }

    [Fact]
    public async Task Does_not_rethrow_a_job_failure_so_the_message_is_not_retried()
    {
        var failing = new FakeJob("collect", () => throw new InvalidOperationException("boom"));

        await CreateFunction(extra: failing).Run("collect", CancellationToken.None);

        Assert.Equal(1, failing.Runs);
    }

    [Fact]
    public async Task Does_not_rethrow_cancellation_so_a_shutdown_does_not_rerun_the_job()
    {
        var cancelled = new FakeJob("collect", () => throw new OperationCanceledException());

        await CreateFunction(extra: cancelled).Run("collect", CancellationToken.None);

        Assert.Equal(1, cancelled.Runs);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Ignores_an_empty_message(string message)
    {
        await CreateFunction().Run(message, CancellationToken.None);

        Assert.Equal(0, _digest.Runs + _alert.Runs);
    }

    [Fact]
    public async Task Checks_the_switch_before_looking_for_the_job()
    {
        var job = new FakeJob("collect");

        await CreateFunction(enabled: false, job).Run("collect", CancellationToken.None);

        Assert.Equal(0, job.Runs);
    }
}
