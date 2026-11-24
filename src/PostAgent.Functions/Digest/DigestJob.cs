using Microsoft.Extensions.Options;
using PostAgent.Shared.Jobs;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;

namespace PostAgent.Digest;

public sealed class DigestJob(
    IPostRepository posts,
    IDigestSummarizer summarizer,
    ITelegramNotifier notifier,
    IOptions<AgentOptions> options,
    TimeProvider time,
    ILogger<DigestJob> log) : IJob
{
    public string Name => "digest";

    public async Task RunAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();

        var recent = await posts.GetSinceAsync(DigestSelector.QueryStart(now), now, ct);
        var items = DigestSelector.Build(recent, options.Value, now);
        if (items.Count == 0)
        {
            await notifier.SendAsync(DigestMessage.NoPosts, ct);
            return;
        }

        var report = (await SummarizeAsync(items, ct)).WithSkipped(DigestSelector.CountSkipped(recent, items, now));

        // Posts are marked only after every message went out: if one fails, the next run repeats the digest
        // (a duplicate is better than a lost digest).
        foreach (var message in DigestMessage.Format(report))
        {
            await notifier.SendAsync(message, ct);
        }

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
