using Microsoft.Azure.Functions.Worker;
using PostAgent.Shared.Infrastructure;
using PostAgent.Shared.Posts.Storage;

namespace PostAgent.Scoring;

public sealed class ScorerFunction(ScoringStage stage)
{
    [Function(nameof(ScorerFunction))]
    public Task Run(
        [QueueTrigger(StorageNames.ScoringQueue, Connection = "AzureWebJobsStorage")] string message,
        CancellationToken ct) =>
        stage.ScoreAsync(PostQueue.Parse(message), ct);
}
