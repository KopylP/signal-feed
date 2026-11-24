using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace PostAgent.Functions;

/// <summary>Читає нові повідомлення джерел, зберігає пости й ставить їх в чергу на оцінювання.</summary>
public sealed class CollectorFunction(
    TelegramSourceReader reader,
    PostRepository posts,
    SourceCursors cursors,
    ScoringQueue queue,
    TelegramNotifier notifier,
    IOptions<AgentOptions> options,
    ILogger<CollectorFunction> log)
{
    private const int MinPauseMs = 1000;
    private const int MaxPauseMs = 2000;
    private const int MaxErrorChars = 150;

    [Function(nameof(CollectorFunction))]
    public async Task Run([TimerTrigger("%CollectorSchedule%")] TimerInfo timer, CancellationToken ct)
    {
        var failures = new List<string>();
        foreach (var (index, username) in options.Value.SourceList.Index())
        {
            if (index > 0)
            {
                await Task.Delay(Random.Shared.Next(MinPauseMs, MaxPauseMs), ct); // не навантажуємо Telegram
            }

            if (await TryCollectAsync(username, ct) is { } failure)
            {
                failures.Add(failure);
            }
        }

        // Без цього збій (наприклад, прострочена сесія) виглядав би як спокійний день.
        if (failures.Count > 0)
        {
            await notifier.SendHealthAsync(failures, ct);
        }
    }

    /// <summary>Помилка одного джерела не зупиняє інші. Повертає опис збою або null.</summary>
    private async Task<string?> TryCollectAsync(string username, CancellationToken ct)
    {
        try
        {
            await CollectAsync(username, ct);
            return null;
        }
        catch (SourceThrottledException ex)
        {
            log.LogWarning("@{Source} skipped until the next run: {Message}", username, ex.Message);
            return null; // очікуваний тимчасовий пропуск, не збій
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Collecting @{Source} failed", username);
            return $"@{username}: {ex.Message.Truncate(MaxErrorChars, "…")}";
        }
    }

    private async Task CollectAsync(string username, CancellationToken ct)
    {
        var batch = await reader.ReadAsync(username, await cursors.GetLastPostIdAsync(username, ct), ct);
        var newPosts = PostBuilder.Build(batch.Source, batch.Messages, options.Value);

        foreach (var post in newPosts)
        {
            await posts.AddIfMissingAsync(post, ct);
        }

        // Порядок важливий: спершу пости й черга, потім LastPostId. Якщо запуск обірветься,
        // наступний прочитає те саме, а Scorer пропустить уже оцінені.
        await queue.EnqueueAsync(newPosts.Select(p => p.ToRef()).ToList(), options.Value.ScoringBatchSize, ct);
        await cursors.SetLastPostIdAsync(username, batch.LastPostId, ct);

        log.LogInformation("@{Source} ({Type}): {Messages} messages, {Posts} posts",
            username, batch.Source.Type, batch.Messages.Count, newPosts.Count);
    }
}
