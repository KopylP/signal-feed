namespace PostAgent.Cleanup;

public static class CleanupServices
{
    // Нове очищення: додайте клас, що реалізує ICleanupTask, і зареєструйте його тут.
    public static IServiceCollection AddCleanup(this IServiceCollection services) =>
        services.AddSingleton<ICleanupTask, PostsCleanup>();
}
