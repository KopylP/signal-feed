using PostAgent.Digest.Infrastructure;
using PostAgent.Shared.Jobs;

namespace PostAgent.Digest;

public static class DigestServices
{
    public static IServiceCollection AddDigest(this IServiceCollection services) =>
        services
            .AddSingleton<IDigestSummarizer, DigestSummarizer>()
            .AddJob<DigestJob>();
}
