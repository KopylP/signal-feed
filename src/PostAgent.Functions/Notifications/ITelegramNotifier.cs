namespace PostAgent.Notifications;

public interface ITelegramNotifier
{
    Task SendAsync(string html, CancellationToken ct);
}
