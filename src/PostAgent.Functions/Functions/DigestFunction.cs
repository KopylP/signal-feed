using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using PostAgent.Services;

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

        var recent = await posts.GetSinceAsync(now - DigestBuilder.FallbackWindow, now, ct);
        var items = DigestBuilder.Build(recent, options.Value, now);
        if (items.Count == 0)
        {
            await notifier.SendNoPostsAsync(ct);
            return;
        }

        await notifier.SendDigestAsync(await SummarizeAsync(items, ct), ct);

        var sent = items.Select(i => i.Post).ToList();
        sent.ForEach(p => p.MarkSent());
        await posts.MarkDeliveredAsync(sent, ct);
        log.LogInformation("Digest sent: {Count} posts", sent.Count);
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
