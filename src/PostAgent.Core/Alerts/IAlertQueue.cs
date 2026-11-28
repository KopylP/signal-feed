namespace PostAgent.Alerts;

public interface IAlertQueue
{
    Task EnqueueAsync(AlertRequest request, CancellationToken ct);
}
