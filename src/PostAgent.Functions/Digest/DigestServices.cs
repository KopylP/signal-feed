namespace PostAgent.Digest;

public static class DigestServices
{
    public static IServiceCollection AddDigest(this IServiceCollection services) =>
        services.AddSingleton<IDigestSummarizer, DigestSummarizer>();
}
