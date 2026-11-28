using PostAgent.Shared.Notifications;
using PostAgent.Shared.Posts;

namespace PostAgent.Alerts;

// Sends a decided alert and marks its posts sent, so the digest does not repeat them (ADR-0024). It does not choose again.
public sealed class AlertSender(IPostRepository posts, ITelegramNotifier notifier, ILogger<AlertSender> log)
{
    public async Task SendAsync(AlertRequest request, CancellationToken ct)
    {
        var found = (await posts.GetAsync(request.Posts.Select(p => new PostRef(p.PartitionKey, p.RowKey)), ct)).ToDictionary(p => p.ToRef());
        var items = request.Posts.Where(p => found.ContainsKey(new PostRef(p.PartitionKey, p.RowKey))).ToList();
        if (items.Count == 0)
        {
            log.LogWarning("The posts of an alert are gone (the cleanup deleted their day), nothing to send");
            return;
        }

        var shown = items.Select(p => found[new PostRef(p.PartitionKey, p.RowKey)]).ToList();
        var text = request.Kind == AlertKind.Group
            ? AlertMessage.Format(shown)
            : WatchAlertMessage.Format(items.Zip(shown, (item, post) => new WatchHit(post, item.Topic ?? string.Empty)).ToList());

        await notifier.SendAsync(text, ct);
        log.LogInformation("Alert sent: {Kind}, {Count} posts", request.Kind, shown.Count);

        // Delivered: a failure from here on must not fail the message, or the queue would send the alert again.
        try
        {
            await posts.MarkSentAsync(shown, alertedAt: null, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "An alert was sent but its posts could not be marked sent, the digest may repeat them");
        }
    }
}
