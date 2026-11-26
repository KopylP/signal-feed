using Microsoft.Extensions.Options;
using PostAgent.Shared.Common;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PostAgent.Pairing;

// /start <key> binds the chat to the bot (ADR-0008). It claims every /start, bound or not, so no other handler sees it.
public sealed class PairingInbox(
    IChatBindingStore store,
    ITelegramNotifier notifier,
    IOptions<AgentOptions> options,
    ILogger<PairingInbox> log) : IIncomingMessageHandler
{
    public async Task<bool> HandleAsync(Update update, CancellationToken ct)
    {
        if (update.Message is not { } message || !StartCommand.IsStart(message.Text))
        {
            return false;
        }

        await BindAsync(message, ct);
        return true;
    }

    private async Task BindAsync(Message message, CancellationToken ct)
    {
        var settings = options.Value;
        if (settings.TelegramChatId != 0)
        {
            log.LogInformation("Pairing ignored: TelegramChatId is set and overrides it");
            return;
        }

        var key = StartCommand.ParseKey(message.Text);
        if (key is null || message.Chat.Type != ChatType.Private)
        {
            return;
        }

        // Nothing is said back for a wrong key, so the bot is not an oracle for guessing it. The key itself is never logged.
        if (!SecretComparison.Matches(settings.TelegramPairingKey, key))
        {
            log.LogWarning("Pairing rejected: wrong key");
            return;
        }

        var chatId = message.Chat.Id;
        var result = await store.TryBindAsync(chatId, settings.TelegramMaxChats, ct);
        switch (result)
        {
            case BindResult.Added:
                log.LogInformation("Chat bound");
                await notifier.SendToAsync(chatId, PairingMessage.Paired, ct);
                break;
            case BindResult.AlreadyBound:
                log.LogInformation("Chat was already bound");
                await notifier.SendToAsync(chatId, PairingMessage.AlreadyPaired, ct);
                break;
            default:
                // The key was right, so telling the sender is not a leak.
                log.LogWarning("Pairing rejected: {Max} chats are bound already", settings.TelegramMaxChats);
                await notifier.SendToAsync(chatId, PairingMessage.Full, ct);
                break;
        }
    }
}
