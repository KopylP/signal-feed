using Microsoft.Extensions.Options;
using PostAgent.Shared.Chat;
using PostAgent.Shared.Settings;

namespace PostAgent.Cleanup;

// Deletes the conversation about a digest older than RetentionDays, so an idle chat does not keep stale data.
// A new digest replaces it anyway; this only matters when digests stop.
public sealed class ChatStateCleanup(IChatStateStore store, IOptions<AgentOptions> options) : ICleanupTask
{
    public string Name => "ChatState";

    public Task<int> RunAsync(DateTimeOffset now, CancellationToken ct) =>
        store.DeleteOlderThanAsync(now.AddDays(-options.Value.RetentionDays), ct);
}
