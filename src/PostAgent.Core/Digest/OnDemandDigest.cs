using PostAgent.Shared.Chat;
using PostAgent.Shared.Common;
using PostAgent.Shared.Feedback;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;

namespace PostAgent.Digest;

// A /digest: the best posts of a period, written by the same summarizer as the daily digest and sent only to the chat that asked.
// It marks nothing as sent and does not replace the conversation, so it never changes what the daily digest or the chat sees.
// Its rating buttons are resolved against its own copy in IRatedDigestStore, since the conversation does not hold it.
public sealed class OnDemandDigest(
    IPostRepository posts,
    IDigestSummarizer summarizer,
    ITelegramNotifier notifier,
    ITelegramPanel panel,
    IRatedDigestStore rated,
    IEffectiveOptions options,
    TimeProvider time,
    ILogger<OnDemandDigest> log)
{
    // Keeps a /digest apart from a daily digest made in the same second.
    private const string ReportIdPrefix = "d";

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

            if (settings.FeedbackEnabled)
            {
                await SendRatingButtonsAsync(request.ChatId, report, ct);
            }
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

    // The digest is already delivered: a failure here is logged and never reaches the reader as "the digest failed".
    private async Task SendRatingButtonsAsync(long chatId, DigestReport report, CancellationToken ct)
    {
        try
        {
            var snapshot = report.ToSnapshot(time.GetUtcNow());
            snapshot = snapshot with { ReportId = ReportIdPrefix + snapshot.ReportId };
            if (DigestFeedbackMessage.Build(snapshot) is not var (html, keyboard))
            {
                return;
            }

            // Saved first: a button without its report could only answer "outdated".
            await rated.SaveAsync(chatId, snapshot, ct);
            await panel.SendAsync(chatId, html, keyboard, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "On-demand digest sent, but the rating buttons could not be sent");
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
