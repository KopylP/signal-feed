using PostAgent.Shared.Chat;
using PostAgent.Shared.Common;
using PostAgent.Shared.Jobs;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Posts;
using PostAgent.Shared.Settings;

namespace PostAgent.Digest;

public sealed class DigestJob(
    IPostRepository posts,
    IDigestSummarizer summarizer,
    ITelegramNotifier notifier,
    IChatStateStore chatState,
    ChatResolver chats,
    IEffectiveOptions options,
    TimeProvider time,
    ILogger<DigestJob> log) : IJob
{
    public string Name => "digest";

    public async Task RunAsync(CancellationToken ct)
    {
        var settings = await options.GetAsync(ct);
        var now = time.GetUtcNow();

        var recent = await posts.GetSinceAsync(DigestSelector.QueryStart(now), now, ct);
        var items = DigestSelector.Build(recent, settings, now);
        var unscored = DigestSelector.CountUnscored(recent, now);
        if (items.Count == 0)
        {
            await notifier.SendAsync(unscored > 0 ? DigestMessage.Unscored(unscored) : DigestMessage.NoPosts, ct);
            return;
        }

        var report = (await SummarizeAsync(items, ct)).WithSkipped(DigestSelector.CountSkipped(recent, items, now));

        // Posts are marked only after every message went out: if one fails, the next run repeats the digest
        // (a duplicate is better than a lost digest).
        foreach (var message in DigestMessage.Format(report, LocalTime.Zone(settings.TimeZone)))
        {
            await notifier.SendAsync(message, ct);
        }

        await posts.MarkSentAsync(items.Select(i => i.Post), alertedAt: null, ct);
        log.LogInformation("Digest sent: {Count} posts", items.Count);

        // First, because it cannot fail the job: the Telegram send below can.
        var started = await StartConversationAsync(report, now, ct);

        // Needs the conversation above: a vote is checked against its snapshot. Also cannot fail the job.
        if (started && settings.FeedbackEnabled)
        {
            await SendFeedbackPromptAsync(report.ToSnapshot(now), ct);
        }

        // After the digest and the marks, so that a failed warning cannot make the next run repeat the digest.
        if (unscored > 0)
        {
            log.LogWarning("{Count} posts were left unscored", unscored);
            await notifier.SendAsync(DigestMessage.Unscored(unscored), ct);
        }
    }

    // The digest is already delivered, so a failure here must not fail the job (the next digest would repeat it).
    private async Task<bool> StartConversationAsync(DigestReport report, DateTimeOffset now, CancellationToken ct)
    {
        try
        {
            var snapshot = report.ToSnapshot(now);
            foreach (var chatId in await chats.GetChatIdsAsync(ct))
            {
                await chatState.StartNewAsync(chatId, snapshot, ct);
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Digest sent, but the chat could not be told about it");
            return false;
        }
    }

    private async Task SendFeedbackPromptAsync(ReportSnapshot snapshot, CancellationToken ct)
    {
        try
        {
            if (DigestFeedbackMessage.Build(snapshot) is var (html, keyboard))
            {
                await notifier.SendAsync(html, keyboard, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Digest sent, but the rating buttons could not be sent");
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
            log.LogWarning(ex, "Digest summary failed, sending the digest without it");
            return DigestReport.Plain(items);
        }
    }
}
