namespace PostAgent.Chat;

public interface IChatQueue
{
    Task EnqueueAsync(ChatRequest request, CancellationToken ct);
}
