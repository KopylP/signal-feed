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
            .AddSingleton<IFeedbackStore, BlobFeedbackStore>()
            .AddSingleton<PromptLibrary>()
            .AddSingleton<IPromptProvider, PromptProvider>()
            .AddSingleton<OverridesChanges>()
            .AddSingleton<SettingsEditor>()
            .AddSingleton<SettingsProposals>();
}
