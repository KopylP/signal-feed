using PostAgent.Chat.Agent;

namespace PostAgent.Tests.Fakes;

internal sealed class RecordingDrafts : IChatDrafts
{
    public List<(long ChatId, long DraftId, string Text)> Sent { get; } = [];

    // Makes every send throw, like a Bot API error or a timeout.
    public bool Fail { get; set; }

    public Task SendAsync(long chatId, long draftId, string text, CancellationToken ct)
    {
        if (Fail)
        {
            throw new IOException("telegram is down");
        }

        Sent.Add((chatId, draftId, text));
        return Task.CompletedTask;
    }
}
