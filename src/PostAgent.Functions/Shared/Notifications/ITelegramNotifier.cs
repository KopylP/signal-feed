namespace PostAgent.Shared.Notifications;

public interface ITelegramNotifier
{
    Task SendAsync(string html, CancellationToken ct);

    // The same message with buttons under it (the collection failure offers to remove a source).
    Task SendAsync(string html, InlineKeyboard keyboard, CancellationToken ct);
}
