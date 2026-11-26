using System.ComponentModel;
using Microsoft.Extensions.AI;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Agent.Tools;

public sealed class ProposeToggleAlertSourceTool : IChatTool
{
    public AIFunction Build(ProposalPlan plan) => AIFunctionFactory.Create(
        ([Description("The source name.")] string name, CancellationToken ct) => plan.AddForExistingSourceAsync(SettingChangeKind.ToggleAlertSource, name, ct),
        new AIFunctionFactoryOptions
        {
            Name = "propose_toggle_alert_source",
            Description = "Plan to switch the alerts for a source on or off (it flips the current state). The name must be one of the current sources from get_settings.",
        });
}
