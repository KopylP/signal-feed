using Microsoft.Extensions.Options;
using PostAgent.Shared.Settings;

namespace PostAgent.Shared.Notifications;

public sealed class ChatResolver(IOptions<AgentOptions> options, IChatBindingStore store)
{
    // TelegramChatId is an explicit override; without it the chat comes from pairing.
    public async Task<long> GetChatIdAsync(CancellationToken ct)
    {
        if (options.Value.TelegramChatId != 0)
        {
            return options.Value.TelegramChatId;
        }

        return await store.GetAsync(ct)
            ?? throw new InvalidOperationException(
                "No Telegram chat is bound yet. Send /start <TelegramPairingKey> to the bot, or set TelegramChatId.");
    }
}
