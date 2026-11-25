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

    public Task<ChatReply> AskAsync(ChatState state, string userText, CancellationToken ct)
    {
        Calls++;
        return ask(state, userText);
    }
}
