using PostAgent.Scoring.Infrastructure;

namespace PostAgent.Scoring;

public static class ScoringServices
{
    public static IServiceCollection AddScoring(this IServiceCollection services) =>
        services.AddSingleton<IPostScorer, PostScorer>();
}
