using PostAgent.Shared.Notifications;

namespace PostAgent.Pairing;

public static class PairingServices
{
    // Registered before every other handler: ServiceCollectionExtensions calls AddPairing first.
    public static IServiceCollection AddPairing(this IServiceCollection services) =>
        services.AddScoped<IIncomingMessageHandler, PairingInbox>();
}
