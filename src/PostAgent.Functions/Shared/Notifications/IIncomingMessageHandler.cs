using Telegram.Bot.Types;

namespace PostAgent.Shared.Notifications;

// Takes an update from the bot's webhook. Lives in Shared so that the webhook and the features that answer (pairing,
// sign-in, the settings menu, the chat) do not reference each other. Handlers are asked in registration order
// until one claims the update; pairing is registered first.
public interface IIncomingMessageHandler
{
    // True when this handler took the update, so the next ones are not asked.
    Task<bool> HandleAsync(Update update, CancellationToken ct);
}
