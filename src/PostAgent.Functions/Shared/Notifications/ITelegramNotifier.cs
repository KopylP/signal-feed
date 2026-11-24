namespace PostAgent.Shared.Notifications;

public interface ITelegramNotifier
{
    Task SendAsync(string html, CancellationToken ct);
}
