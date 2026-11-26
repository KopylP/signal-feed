using Microsoft.Extensions.AI;
using PostAgent.Shared.Settings;

namespace PostAgent.Shared.Ai;

// The client is a singleton, but the owner can change the model from the bot: the model is set on every request from the effective options.
public sealed class ModelSelectingChatClient(IChatClient inner, IEffectiveOptions effective, Func<AgentOptions, string> pickModel)
    : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        await base.GetResponseAsync(messages, await WithModelAsync(options, cancellationToken), cancellationToken);

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in base.GetStreamingResponseAsync(messages, await WithModelAsync(options, cancellationToken), cancellationToken))
        {
            yield return update;
        }
    }

    private async Task<ChatOptions> WithModelAsync(ChatOptions? options, CancellationToken ct)
    {
        var copy = options?.Clone() ?? new ChatOptions();
        copy.ModelId = pickModel(await effective.GetAsync(ct));
        return copy;
    }
}
