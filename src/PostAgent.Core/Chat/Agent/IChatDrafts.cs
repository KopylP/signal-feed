namespace PostAgent.Chat.Agent;

// A draft is a temporary preview of an answer that is still being written (Bot API sendMessageDraft): it shows in the chat for about
// 30 seconds and goes away when the real message arrives. Same draft id: the text is animated; another id: it is replaced.
public interface IChatDrafts
{
    Task SendAsync(long chatId, long draftId, string text, CancellationToken ct);
}
