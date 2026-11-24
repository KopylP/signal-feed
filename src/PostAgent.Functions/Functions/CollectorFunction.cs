using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using PostAgent.Services;
using PostAgent.Telegram;
using TL;

namespace PostAgent.Functions;

/// <summary>Читає нові повідомлення джерел, зберігає пости й ставить їх в чергу на оцінювання.</summary>
public sealed class CollectorFunction(
    TelegramSourceReader reader,
    PostRepository posts,
    ScoringQueue queue,
    IOptions<AgentOptions> options,
    ILogger<CollectorFunction> log)
{
    private const int MinPauseMs = 1000;
    private const int MaxPauseMs = 2000;

    [Function(nameof(CollectorFunction))]
    public async Task Run([TimerTrigger("%CollectorSchedule%")] TimerInfo timer, CancellationToken ct)
    {
        foreach (var username in options.Value.SourceList)
        {
            await TryCollectAsync(username, ct);
            await Task.Delay(Random.Shared.Next(MinPauseMs, MaxPauseMs), ct); // не навантажуємо Telegram
        }
    }

    /// <summary>Помилка одного джерела не зупиняє інші.</summary>
    private async Task TryCollectAsync(string username, CancellationToken ct)
    {
        try
        {
            await CollectAsync(username, ct);
        }
        catch (RpcException ex) when (ex.Message.StartsWith("FLOOD_WAIT"))
        {
            log.LogWarning("@{Source} skipped until the next run: {Message}", username, ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Collecting @{Source} failed", username);
        }
    }

    private async Task CollectAsync(string username, CancellationToken ct)
    {
        var batch = await reader.ReadAsync(username, await posts.GetLastPostIdAsync(username, ct), ct);
        var newPosts = PostBuilder.Build(batch.Source, batch.Messages, options.Value);

        foreach (var post in newPosts)
        {
            await posts.AddIfMissingAsync(post, ct);
        }

        // Порядок важливий: спершу пости й черга, потім LastPostId. Якщо запуск обірветься,
        // наступний прочитає те саме, а Scorer пропустить уже оцінені.
        await queue.EnqueueAsync(newPosts.Select(p => p.ToRef()).ToList(), options.Value.ScoringBatchSize, ct);
        await posts.SetLastPostIdAsync(username, batch.LastPostId, ct);

        log.LogInformation("@{Source} ({Type}): {Messages} messages, {Posts} posts",
            username, batch.Source.Type, batch.Messages.Count, newPosts.Count);
    }
}
