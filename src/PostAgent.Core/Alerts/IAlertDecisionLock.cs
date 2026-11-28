namespace PostAgent.Alerts;

// One alert decision at a time across all instances, so the cooldown of a group and the mark of a post hold across parallel batches.
public interface IAlertDecisionLock
{
    // Waits while another decision holds it; disposing the handle releases it.
    Task<IAsyncDisposable> AcquireAsync(CancellationToken ct);
}
