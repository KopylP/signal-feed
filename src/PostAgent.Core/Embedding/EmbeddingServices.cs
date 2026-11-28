using PostAgent.Embedding.Infrastructure;

namespace PostAgent.Embedding;

public static class EmbeddingServices
{
    public static IServiceCollection AddEmbedding(this IServiceCollection services) =>
        services
            .AddSingleton<IEmbeddingLock, BlobEmbeddingLock>()
            .AddSingleton<PostEmbedder>();
}
