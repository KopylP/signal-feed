using Microsoft.Extensions.AI;
using PostAgent.Shared.Stats;

namespace PostAgent.Shared.Ai;

// Counts the tokens of every model call per UTC day and purpose, for /stats. Below the function-calling layer, so each call of a
// tool round is counted once. Recording has its own short timeout and never uses the caller's token: a call that was paid for is counted
// even when the caller is cancelled, and a failure to record never touches the call.
public sealed class UsageRecordingChatClient(IChatClient inner, IUsageStore store, TimeProvider time, string purpose, ILogger<UsageRecordingChatClient> log)
    : DelegatingChatClient(inner)
{
    private static readonly TimeSpan RecordTimeout = TimeSpan.FromSeconds(5);

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken);
        await RecordAsync(response.Usage);
        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        UsageDetails? total = null;
        try
        {
            await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
            {
                foreach (var usage in update.Contents.OfType<UsageContent>())
                {
                    total ??= new UsageDetails();
                    total.Add(usage.Details);
                }

                yield return update;
            }
        }
        finally
        {
            // Also when the consumer stops early or the stream fails: the tokens of what was streamed are billed.
            await RecordAsync(total);
        }
    }

    private async Task RecordAsync(UsageDetails? usage)
    {
        if (usage is null)
        {
            return;
        }

        try
        {
            using var timeout = new CancellationTokenSource(RecordTimeout);
            await store.RecordAsync(DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime), purpose, usage.InputTokenCount ?? 0, usage.OutputTokenCount ?? 0, timeout.Token);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "The token usage of a model call could not be recorded");
        }
    }
}
