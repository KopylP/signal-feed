using PostAgent.Shared.Ai;
using PostAgent.Shared.Feedback;
using PostAgent.Shared.Feedback.Infrastructure;
using PostAgent.Shared.Settings.Infrastructure;

namespace PostAgent.Shared.Settings;

public static class SettingsServices
{
    public static IServiceCollection AddSettings(this IServiceCollection services) =>
        services
            .AddSingleton<IOverridesStore, BlobOverridesStore>()
            .AddSingleton<IEffectiveOptions, EffectiveOptions>()
            .AddSingleton<IProfileStore, BlobProfileStore>()
            .AddSingleton<IFeedbackStore, BlobFeedbackStore>()
            .AddSingleton<IRatedDigestStore, BlobRatedDigestStore>()
            .AddSingleton<PromptLibrary>()
            .AddSingleton<IPromptProvider, PromptProvider>()
            .AddSingleton<SettingsEditor>();
}
