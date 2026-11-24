using Microsoft.Extensions.Options;
using PostAgent.Shared.Common;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Settings;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace PostAgent.Pairing;

public sealed class PairingService(
    IChatBindingStore store,
    ITelegramNotifier notifier,
    IOptions<AgentOptions> options,
    ILogger<PairingService> log)
{
    public async Task HandleAsync(Message message, CancellationToken ct)
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
        var bound = await store.GetAsync(ct);
        var justPaired = bound is null && await store.TryBindAsync(chatId, ct);
        if (justPaired)
        {
            bound = chatId;
        }
        else if (bound is null)
        {
            // Lost a race with another pairing: whoever won is the bound chat.
            bound = await store.GetAsync(ct);
        }

        if (bound != chatId)
        {
            log.LogWarning("Pairing rejected: the agent is already bound to another chat");
            return;
        }

        log.LogInformation(justPaired ? "Chat bound" : "Chat was already bound");
        await notifier.SendAsync(justPaired ? PairingMessage.Paired : PairingMessage.AlreadyPaired, ct);
    }
}
