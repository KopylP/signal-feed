using System.Text.Json;
using PostAgent.Chat;
using PostAgent.Shared.Chat;

namespace PostAgent.Tests.Fakes;

internal sealed class FakeChatAgent(Func<ChatState, string, Task<ChatReply>> ask) : IChatAgent
{
    public FakeChatAgent(Func<string, string> answer)
        : this((_, text) => Task.FromResult(new ChatReply(answer(text), Session())))
    {
    }

    public int Calls { get; private set; }

    public static JsonElement Session() => JsonDocument.Parse("{}").RootElement.Clone();

    public long? LastChatId { get; private set; }

    public Task<ChatReply> AskAsync(long chatId, ChatState state, string userText, CancellationToken ct)
    {
        Calls++;
        LastChatId = chatId;
        return ask(state, userText);
    }
}
