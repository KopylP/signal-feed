using PostAgent.Shared.Common;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;

namespace PostAgent.Digest;

// A /digest: the best posts of a period, written by the same summarizer as the daily digest and sent only to the chat that asked.
// It marks nothing as sent and does not replace the conversation, so it never changes what the daily digest or the chat sees.
public sealed class OnDemandDigest(
    IPostRepository posts,
    IDigestSummarizer summarizer,
    ITelegramNotifier notifier,
    IEffectiveOptions options,
    ILogger<OnDemandDigest> log)
{
    public async Task RunAsync(DigestRequest request, CancellationToken ct)
    {
        try
        {
            var settings = await options.GetAsync(ct);
            var zone = LocalTime.Zone(settings.TimeZone);
            var period = new PostPeriod(request.Since, request.Until);
            var label = $"період {period.Describe(zone)}";

            var recent = await posts.GetSinceAsync(period.Since, period.Until, ct);
            var items = DigestSelector.BuildForPeriod(recent, settings, period);
            if (items.Count == 0)
            {
                await notifier.SendToAsync(request.ChatId, DigestTexts.Empty(label), ct);
                return;
            }

            var report = await SummarizeAsync(items, ct);
            foreach (var message in DigestMessage.Format(report, zone))
            {
                await notifier.SendToAsync(request.ChatId, message, ct);
            }

            log.LogInformation("On-demand digest sent: {Count} posts", items.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Not retried: a retry could send half a digest twice. The reader can ask again.
            log.LogError(ex, "An on-demand digest failed");
            await TrySendFailureAsync(request.ChatId, ct);
        }
    }

    private async Task<DigestReport> SummarizeAsync(IReadOnlyList<DigestItem> items, CancellationToken ct)
    {
        try
        {
            return await summarizer.SummarizeAsync(items, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "The on-demand digest summary failed, sending the posts without it");
            return DigestReport.Plain(items);
        }
    }

    private async Task TrySendFailureAsync(long chatId, CancellationToken ct)
    {
        try
        {
            await notifier.SendToAsync(chatId, DigestTexts.Failed, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "The failure of an on-demand digest could not be told to the reader");
        }
    }
}
