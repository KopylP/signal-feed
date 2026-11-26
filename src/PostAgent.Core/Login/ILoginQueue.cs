namespace PostAgent.Login;

public interface ILoginQueue
{
    Task EnqueueAsync(LoginRequest request, CancellationToken ct);
}
