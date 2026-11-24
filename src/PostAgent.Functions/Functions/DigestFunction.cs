using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using PostAgent.Services;

namespace PostAgent.Functions;

/// <summary>Раз на день надсилає дайджест найкращих постів. Дайджест приходить завжди.</summary>
public sealed class DigestFunction(
    PostRepository posts,
    TelegramNotifier notifier,
    IOptions<AgentOptions> options,
    TimeProvider time,
    ILogger<DigestFunction> log)
{
    [Function(nameof(DigestFunction))]
    public async Task Run([TimerTrigger("%DigestSchedule%")] TimerInfo timer, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await posts.DeleteOlderThanAsync(now.AddDays(-options.Value.RetentionDays), ct);

        var recent = await posts.GetSinceAsync(now - DigestBuilder.FallbackWindow, now, ct);
        var items = DigestBuilder.Build(recent, options.Value, now);
        if (items.Count == 0)
        {
            await notifier.SendNoPostsAsync(ct);
            return;
        }

        await notifier.SendDigestAsync(items, ct);

        var sent = items.Select(i => i.Post).ToList();
        sent.ForEach(p => p.MarkSent());
        await posts.UpdateAllAsync(sent, ct);
        log.LogInformation("Digest sent: {Count} posts", sent.Count);
    }
}
