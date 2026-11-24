using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using PostAgent.Models;
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
        var settings = options.Value;
        var now = time.GetUtcNow();

        var recent = await posts.GetSinceAsync(now.AddHours(-48), now, ct);
        var items = DigestBuilder.Build(recent, settings, now);

        if (items.Count == 0)
        {
            await notifier.SendNoPostsAsync(ct);
        }
        else
        {
            await notifier.SendDigestAsync(items, ct);
            foreach (var item in items)
            {
                item.Post.Status = PostStatus.Sent;
                await posts.UpdateAsync(item.Post, ct);
            }
        }

        log.LogInformation("Digest sent: {Count} posts", items.Count);
        await posts.DeleteOlderThanAsync(now.AddDays(-settings.RetentionDays), ct);
    }
}
