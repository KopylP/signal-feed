using PostAgent.Shared.Chat;

namespace PostAgent.Chat;

public interface IChatAgent
{
    // Throws when the model fails or returns no text; the caller decides what the reader sees.
    Task<ChatReply> AskAsync(ChatState state, string userText, CancellationToken ct);
}
