using Telegram.Bot.Types;

namespace PostAgent.Shared.Notifications;

// Takes an update from the bot's webhook that is not a pairing command. Lives in Shared so that the pairing feature
// (which owns the webhook) and the features that answer (the settings menu, the chat) do not reference each other.
// Handlers are asked in registration order until one claims the update.
public interface IIncomingMessageHandler
{
    // True when this handler took the update, so the next ones are not asked.
    Task<bool> HandleAsync(Update update, CancellationToken ct);
}
