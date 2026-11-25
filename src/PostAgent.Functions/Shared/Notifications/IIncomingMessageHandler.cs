using Telegram.Bot.Types;

namespace PostAgent.Shared.Notifications;

// Takes a message from the bot's webhook that is not a pairing command. Lives in Shared so that the pairing feature
// (which owns the webhook) and the chat feature (which answers) do not reference each other.
public interface IIncomingMessageHandler
{
    Task HandleAsync(Update update, CancellationToken ct);
}
