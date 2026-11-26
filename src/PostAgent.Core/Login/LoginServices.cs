using PostAgent.Login.Infrastructure;
using PostAgent.Shared.Notifications;

namespace PostAgent.Login;

public static class LoginServices
{
    // Register before the settings menu and the chat, so that /login works even while the settings storage is down.
    public static IServiceCollection AddLogin(this IServiceCollection services) =>
        services
            .AddSingleton<ILoginAttemptStore, BlobLoginAttemptStore>()
            .AddSingleton<ISessionStore, BlobSessionStore>()
            .AddSingleton<ILoginQueue, LoginQueue>()
            .AddSingleton<IUserLoginFactory, WTelegramUserLoginFactory>()
            .AddScoped<IIncomingMessageHandler, LoginInbox>()
            .AddScoped<LoginProcessor>()
            .AddScoped<LoginForm>();
}
