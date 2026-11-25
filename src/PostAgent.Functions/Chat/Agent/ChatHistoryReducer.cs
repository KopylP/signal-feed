using Microsoft.Extensions.AI;

namespace PostAgent.Chat.Agent;

// Caps a conversation at max messages. The first message carries the digest, so it is never dropped.
// The built-in MessageCountingChatReducer is experimental, and a plain count can keep a tool result without the call
// that asked for it, which OpenAI rejects.
public sealed class ChatHistoryReducer(int max) : IChatReducer
{
    public Task<IEnumerable<ChatMessage>> ReduceAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken)
    {
        var list = messages.ToList();
        if (list.Count <= max)
        {
            return Task.FromResult<IEnumerable<ChatMessage>>(list);
        }

        var tail = list.Skip(list.Count - (max - 1)).SkipWhile(m => m.Role == ChatRole.Tool);
        return Task.FromResult<IEnumerable<ChatMessage>>([list[0], .. tail]);
    }
}
