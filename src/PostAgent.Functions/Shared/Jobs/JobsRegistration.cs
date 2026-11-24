namespace PostAgent.Shared.Jobs;

public static class JobsRegistration
{
    // Registers the job under its own type (for its timer function) and as IJob (for run-now).
    public static IServiceCollection AddJob<TJob>(this IServiceCollection services) where TJob : class, IJob =>
        services
            .AddScoped<TJob>()
            .AddScoped<IJob>(sp => sp.GetRequiredService<TJob>());
}
