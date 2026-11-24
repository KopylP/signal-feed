using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace PostAgent.Functions;

/// <summary>
/// Раз на день надсилає дайджест: підсумок від моделі, що саме цікаве, і найкращі пости з посиланнями.
/// Дайджест приходить завжди: якщо модель недоступна, він іде без підсумку.
/// </summary>
public sealed class DigestFunction(
    PostRepository posts,
    DigestSummarizer summarizer,
    TelegramNotifier notifier,
    IOptions<AgentOptions> options,
    TimeProvider time,
    ILogger<DigestFunction> log)
{
    [Function(nameof(DigestFunction))]
    public async Task Run([TimerTrigger("%DigestSchedule%")] TimerInfo timer, CancellationToken ct)
    {
        var now = time.GetUtcNow();

        var recent = await posts.GetSinceAsync(DigestBuilder.QueryStart(now), now, ct);
        var items = DigestBuilder.Build(recent, options.Value, now);
        if (items.Count == 0)
        {
            await notifier.SendNoPostsAsync(ct);
            return;
        }

        await notifier.SendDigestAsync(await SummarizeAsync(items, ct), ct);

        await posts.MarkSentAsync(items.Select(i => i.Post), alertedAt: null, ct);
        log.LogInformation("Digest sent: {Count} posts", items.Count);
    }

    private async Task<DigestReport> SummarizeAsync(IReadOnlyList<DigestItem> items, CancellationToken ct)
    {
        try
        {
            return await summarizer.SummarizeAsync(items, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Digest summary failed, sending the digest without it");
            return DigestReport.Plain(items);
        }
    }
}
