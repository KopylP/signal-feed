using PostAgent.Digest.Infrastructure;
using PostAgent.Shared.Jobs;
using PostAgent.Shared.Notifications;

namespace PostAgent.Digest;

public static class DigestServices
{
    public static IServiceCollection AddDigest(this IServiceCollection services) =>
        services
            .AddSingleton<IDigestSummarizer, DigestSummarizer>()
            .AddSingleton<IDigestRequestQueue, DigestRequestQueue>()
            .AddScoped<OnDemandDigest>()
            .AddScoped<IIncomingMessageHandler, DigestInbox>()
            .AddJob<DigestJob>();
}
