using PostAgent.Shared.Notifications;

namespace PostAgent.SettingsMenu;

public static class SettingsMenuServices
{
    // Scoped, like the other handlers: the panel is a typed HTTP client. Register before AddChat so that the menu is asked first.
    public static IServiceCollection AddSettingsMenu(this IServiceCollection services) =>
        services
            .AddSingleton<MenuState>()
            .AddScoped<MenuController>()
            .AddScoped<IIncomingMessageHandler, SettingsInbox>();
}
