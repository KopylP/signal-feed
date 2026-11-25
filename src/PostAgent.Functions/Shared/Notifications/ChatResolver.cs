using Microsoft.Extensions.Options;
using PostAgent.Shared.Settings;

namespace PostAgent.Shared.Notifications;

public sealed class ChatResolver(IOptions<AgentOptions> options, IChatBindingStore store)
{
    // TelegramChatId is an explicit override; without it the chat comes from pairing. Null while nothing is bound.
    public async Task<long?> TryGetChatIdAsync(CancellationToken ct) =>
        options.Value.TelegramChatId != 0 ? options.Value.TelegramChatId : await store.GetAsync(ct);

    public async Task<long> GetChatIdAsync(CancellationToken ct)
    {
        return await TryGetChatIdAsync(ct)
            ?? throw new InvalidOperationException(
                "No Telegram chat is bound yet. Send /start <TelegramPairingKey> to the bot, or set TelegramChatId.");
    }
}
