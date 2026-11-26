using Microsoft.Extensions.Options;
using PostAgent.Shared.Settings;

namespace PostAgent.Shared.Notifications;

public sealed class ChatResolver(IOptions<AgentOptions> options, IChatBindingStore store)
{
    // TelegramChatId is an explicit override and means exactly one chat; without it the chats come from pairing. Empty while nothing is bound.
    public async Task<IReadOnlyList<long>> GetChatIdsAsync(CancellationToken ct) =>
        options.Value.TelegramChatId != 0 ? [options.Value.TelegramChatId] : await store.GetAllAsync(ct);

    public async Task<bool> IsBoundAsync(long chatId, CancellationToken ct) =>
        (await GetChatIdsAsync(ct)).Contains(chatId);

    public async Task<IReadOnlyList<long>> RequireChatIdsAsync(CancellationToken ct)
    {
        var chats = await GetChatIdsAsync(ct);
        return chats.Count > 0
            ? chats
            : throw new InvalidOperationException(
                "No Telegram chat is bound yet. Send /start <TelegramPairingKey> to the bot, or set TelegramChatId.");
    }
}
