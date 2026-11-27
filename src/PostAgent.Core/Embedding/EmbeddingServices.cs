namespace PostAgent.Embedding;

public static class EmbeddingServices
{
    public static IServiceCollection AddEmbedding(this IServiceCollection services) =>
        services.AddSingleton<PostEmbedder>();
}
