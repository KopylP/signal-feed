using System.ComponentModel;
using Microsoft.Extensions.AI;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Agent.Tools;

public sealed class ProposeResetSettingTool : IChatTool
{
    public AIFunction Build(ChatTurn turn) => AIFunctionFactory.Create(
        ([Description("The setting key.")] string key, CancellationToken ct) => RunAsync(turn.Plan, key, ct),
        new AIFunctionFactoryOptions { Name = "propose_reset_setting", Description = "Plan to put one setting back to the value from the app settings." });

    private static Task<string> RunAsync(ProposalPlan plan, string key, CancellationToken ct) =>
        SettingsCatalog.Find(key.Trim()) is null
            ? Task.FromResult("Unknown setting key. Call get_settings.")
            : plan.AddAsync(new SettingChange(SettingChangeKind.Reset, key.Trim()), ct);
}
