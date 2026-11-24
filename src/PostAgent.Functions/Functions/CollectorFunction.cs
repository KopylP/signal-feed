using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using PostAgent.Models;
using PostAgent.Services;
using PostAgent.Telegram;
using TL;

namespace PostAgent.Functions;

/// <summary>Читає нові повідомлення джерел, зберігає пости й ставить їх в чергу на оцінювання.</summary>
public sealed class CollectorFunction(
    IServiceScopeFactory scopes,
    PostRepository posts,
    ScoringQueue queue,
    IOptions<AgentOptions> options,
    ILogger<CollectorFunction> log)
{
    [Function(nameof(CollectorFunction))]
    public async Task Run([TimerTrigger("%CollectorSchedule%")] TimerInfo timer, CancellationToken ct)
    {
        var settings = options.Value;

        // Окремий scope на запуск: клієнт Telegram відкривається й закривається разом із ним.
        await using var scope = scopes.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<TelegramSourceReader>();

        var sources = settings.SourceList;
        for (var i = 0; i < sources.Count; i++)
        {
            if (i > 0)
            {
                await Task.Delay(Random.Shared.Next(1000, 2000), ct);
            }

            try
            {
                await CollectAsync(reader, sources[i], settings, ct);
            }
            catch (RpcException ex) when (ex.Message.StartsWith("FLOOD_WAIT"))
            {
                log.LogWarning("@{Source} skipped until the next run: {Message}", sources[i], ex.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Collecting @{Source} failed", sources[i]);
            }
        }
    }

    private async Task CollectAsync(TelegramSourceReader reader, string username, AgentOptions settings, CancellationToken ct)
    {
        var lastPostId = await posts.GetLastPostIdAsync(username, ct);
        var batch = await reader.ReadAsync(username, lastPostId, ct);

        var newPosts = PostBuilder.Build(batch.Source, batch.Messages, settings);
        foreach (var post in newPosts)
        {
            await posts.AddIfMissingAsync(post, ct);
        }

        // Порядок важливий: спершу пости й черга, потім LastPostId. Якщо запуск обірветься,
        // наступний прочитає те саме, а Scorer пропустить уже оцінені.
        await queue.EnqueueAsync(newPosts.Select(p => p.ToRef()).ToList(), settings.ScoringBatchSize, ct);
        await posts.SetLastPostIdAsync(username, batch.LastPostId, ct);

        log.LogInformation("@{Source} ({Type}): {Messages} messages, {Posts} posts", username, batch.Source.Type,
            batch.Messages.Count, newPosts.Count);
    }
}
