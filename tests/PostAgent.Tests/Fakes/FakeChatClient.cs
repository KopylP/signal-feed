using Microsoft.Extensions.AI;

namespace PostAgent.Tests.Fakes;

internal sealed class FakeChatClient(string responseJson) : IChatClient
{
    public List<ChatMessage> Messages { get; } = [];
    public ChatOptions? Options { get; private set; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Messages.AddRange(messages);
        Options = options;
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseJson)));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
