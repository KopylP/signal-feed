using Microsoft.Extensions.AI;

namespace PostAgent.Tests.Fakes;

// Returns the scripted responses one by one (the last one repeats) and records what it was sent.
// FakeChatClient returns one fixed JSON and cannot ask for a tool.
internal sealed class ScriptedChatClient(params Func<IReadOnlyList<ChatMessage>, ChatResponse>[] script) : IChatClient
{
    public List<List<ChatMessage>> Calls { get; } = [];
    public ChatOptions? LastOptions { get; private set; }

    // What a real model does once the function-calling library takes the tools away: it answers in text.
    public string? TextWhenNoTools { get; init; }

    public static Func<IReadOnlyList<ChatMessage>, ChatResponse> Reply(string text) =>
        _ => new ChatResponse(new ChatMessage(ChatRole.Assistant, text));

    public static Func<IReadOnlyList<ChatMessage>, ChatResponse> CallTool(string name, string argumentName, string argumentValue) =>
        _ => new ChatResponse(new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent(Guid.NewGuid().ToString("N"), name, new Dictionary<string, object?> { [argumentName] = argumentValue })]));

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Calls.Add(messages.ToList());
        LastOptions = options;
        if (TextWhenNoTools is not null && options?.Tools is not { Count: > 0 })
        {
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, TextWhenNoTools)));
        }

        return Task.FromResult(script[Math.Min(Calls.Count - 1, script.Length - 1)](Calls[^1]));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
