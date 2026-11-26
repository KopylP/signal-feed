using System.ComponentModel;
using Microsoft.Extensions.AI;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Agent.Tools;

public sealed class ProposeRemoveSourceTool : IChatTool
{
    public AIFunction Build(ChatTurn turn) => AIFunctionFactory.Create(
        ([Description("The source name.")] string name, CancellationToken ct) => turn.Plan.AddForExistingSourceAsync(SettingChangeKind.RemoveSource, name, ct),
        new AIFunctionFactoryOptions
        {
            Name = "propose_remove_source",
            Description = "Plan to remove a source. The name must be one of the current sources from get_settings; if the reader was not specific, ask which one and offer the current sources with offer_choices.",
        });
}
