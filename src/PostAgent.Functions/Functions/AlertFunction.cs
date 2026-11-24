using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using PostAgent.Models;
using PostAgent.Services;

namespace PostAgent.Functions;

/// <summary>Щогодини шукає в групах зі списку AlertSources сплески й дуже важливі повідомлення.</summary>
public sealed class AlertFunction(
    PostRepository posts,
    TelegramNotifier notifier,
    IOptions<AgentOptions> options,
    TimeProvider time,
    ILogger<AlertFunction> log)
{
    [Function(nameof(AlertFunction))]
    public async Task Run([TimerTrigger("%AlertSchedule%")] TimerInfo timer, CancellationToken ct)
    {
        var settings = options.Value;
        var now = time.GetUtcNow();

        if (QuietHours.IsQuiet(settings.AlertQuietHours, settings.AlertTimeZone, now))
        {
            log.LogInformation("Quiet hours, alerts skipped (posts stay for the digest)");
            return;
        }

        // Вікно оцінених постів плюс cooldown: AlertedAt шукаємо серед нещодавніх постів.
        var since = now.AddHours(-(settings.AlertWindowHours + settings.AlertCooldownHours));
        var alerts = AlertSelector.Select(await posts.GetSinceAsync(since, now, ct), settings, now);
        if (alerts.Count == 0)
        {
            return;
        }

        await notifier.SendAlertAsync(alerts, ct);
        foreach (var post in alerts)
        {
            post.Status = PostStatus.Sent; // у дайджесті такі пости не повторюються
            post.AlertedAt = now;
            await posts.UpdateAsync(post, ct);
        }

        log.LogInformation("Alert sent: {Count} posts", alerts.Count);
    }
}
