namespace PostAgent.Pairing;

public static class PairingServices
{
    public static IServiceCollection AddPairing(this IServiceCollection services) =>
        services
            .AddScoped<PairingService>()
            .AddScoped<PairingWebhook>();
}
