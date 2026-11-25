using Microsoft.Extensions.Options;
using PostAgent.Chat.Agent;
using PostAgent.Shared.Settings;
using Telegram.Bot;

namespace PostAgent.Chat.Infrastructure;

public sealed class TelegramChatDrafts(HttpClient http, IOptions<AgentOptions> options) : IChatDrafts
{
    private readonly TelegramBotClient _bot = new(options.Value.TelegramBotToken, http);

    // Plain text, no parse mode: the model writes plain text, and half-written HTML would be refused.
    public Task SendAsync(long chatId, long draftId, string text, CancellationToken ct) =>
        _bot.SendMessageDraft(chatId, draftId, text, cancellationToken: ct);
}
