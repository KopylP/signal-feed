using Microsoft.Extensions.AI;
using PostAgent.Shared.Stats;

namespace PostAgent.Shared.Ai;

// Counts the tokens of every model call per UTC day and purpose, for /stats. Below the function-calling layer, so each call of a
// tool round is counted once. A failure to record never touches the call itself.
public sealed class UsageRecordingChatClient(IChatClient inner, IUsageStore store, TimeProvider time, string purpose, ILogger<UsageRecordingChatClient> log)
    : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken);
        await RecordAsync(response.Usage, cancellationToken);
        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        UsageDetails? total = null;
        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            foreach (var usage in update.Contents.OfType<UsageContent>())
            {
                total ??= new UsageDetails();
                total.Add(usage.Details);
            }

            yield return update;
        }

        await RecordAsync(total, cancellationToken);
    }

    private async Task RecordAsync(UsageDetails? usage, CancellationToken ct)
    {
        if (usage is null)
        {
            return;
        }

        try
        {
            await store.RecordAsync(DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime), purpose, usage.InputTokenCount ?? 0, usage.OutputTokenCount ?? 0, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "The token usage of a model call could not be recorded");
        }
    }
}
