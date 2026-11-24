namespace PostAgent.Scoring;

public static class ScoringServices
{
    public static IServiceCollection AddScoring(this IServiceCollection services) =>
        services
            .AddSingleton<IScoringQueue, ScoringQueue>()
            .AddSingleton<IPostScorer, PostScorer>();
}
