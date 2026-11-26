using System.ComponentModel;
using Microsoft.Extensions.AI;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Agent.Tools;

public sealed class ProposeRemoveWatchTool(SettingsEditor editor) : IChatTool
{
    public AIFunction Build(ProposalPlan plan) => AIFunctionFactory.Create(
        ([Description("The followed topic exactly as get_settings lists it.")] string phrase, CancellationToken ct) => RunAsync(plan, phrase, ct),
        new AIFunctionFactoryOptions
        {
            Name = "propose_remove_watch",
            Description = "Plan to stop following a topic. The phrase must be one of the followed topics from get_settings.",
        });

    // A made-up phrase is sent back to the list instead of failing later.
    private async Task<string> RunAsync(ProposalPlan plan, string phrase, CancellationToken ct)
    {
        var normalized = WatchPhrases.Normalize(phrase);
        var current = await editor.CurrentWatchesAsync(ct);
        return normalized is not null && current.FirstOrDefault(w => w.Equals(normalized, StringComparison.OrdinalIgnoreCase)) is { } existing
            ? await plan.AddAsync(new SettingChange(SettingChangeKind.RemoveWatch, existing), ct)
            : "That topic is not followed. Call get_settings and ask the reader which one they mean.";
    }
}
