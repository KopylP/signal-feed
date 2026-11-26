namespace PostAgent.Shared.Notifications;

public interface ITelegramNotifier
{
    // To every bound chat (ADR-0014).
    Task SendAsync(string html, CancellationToken ct);

    // The same message with buttons under it (the collection failure offers to remove a source).
    Task SendAsync(string html, InlineKeyboard keyboard, CancellationToken ct);

    // To one chat: the answer to its question, or the reply to its pairing.
    Task SendToAsync(long chatId, string html, CancellationToken ct);
}
