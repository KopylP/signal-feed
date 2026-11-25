using System.ComponentModel;
using Microsoft.Extensions.AI;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Agent.Tools;

public sealed class ProposeResetGroupTool : IChatTool
{
    public AIFunction Build(ProposalPlan plan) => AIFunctionFactory.Create(
        ([Description("The group name.")] string group, CancellationToken ct) => RunAsync(plan, group, ct),
        new AIFunctionFactoryOptions
        {
            Name = "propose_reset_group",
            Description = "Plan to put a whole group of settings (Sources, Digest, Alerts, Collecting, Chat, Models) back to the values from the app settings.",
        });

    private static Task<string> RunAsync(ProposalPlan plan, string group, CancellationToken ct) =>
        Enum.TryParse<SettingGroup>(group.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? plan.AddAsync(new SettingChange(SettingChangeKind.ResetGroup, parsed.ToString()), ct)
            : Task.FromResult("Unknown group. Use one of: " + string.Join(", ", Enum.GetNames<SettingGroup>()));
}
