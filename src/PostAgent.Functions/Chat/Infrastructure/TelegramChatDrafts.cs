using Microsoft.Extensions.Options;
using PostAgent.Chat.Agent;
using PostAgent.Shared.Settings;
using Telegram.Bot;

namespace PostAgent.Chat.Infrastructure;

public sealed class TelegramChatDrafts(HttpClient http, IOptions<AgentOptions> options) : IChatDrafts
{
    // No retry inside the library either (its default waits out a "too many requests" for up to a minute): a draft is awaited by the
    // model loop, so a slow or refused draft must end quickly and turn the preview off, never slow the answer.
    private readonly TelegramBotClient _bot = new(new TelegramBotClientOptions(options.Value.TelegramBotToken) { RetryCount = 0 }, http);

    // Plain text, no parse mode: the model writes plain text, and half-written HTML would be refused.
    public Task SendAsync(long chatId, long draftId, string text, CancellationToken ct) =>
        _bot.SendMessageDraft(chatId, draftId, text, cancellationToken: ct);
}
