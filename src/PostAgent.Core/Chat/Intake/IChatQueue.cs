namespace PostAgent.Chat.Intake;

public interface IChatQueue
{
    Task EnqueueAsync(ChatRequest request, CancellationToken ct);
}
