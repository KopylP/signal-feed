using System.ComponentModel;
using Microsoft.Extensions.AI;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Infrastructure.Tools;

public sealed class ProposeProfileEditTool : IChatTool
{
    public AIFunction Build(ProposalPlan plan) => AIFunctionFactory.Create(
        (
            [Description("The fragment to replace, copied exactly.")] string old_text,
            [Description("The new text.")] string new_text,
            CancellationToken ct) => plan.AddAsync(new SettingChange(SettingChangeKind.EditProfile, "profile", new_text, old_text), ct),
        new AIFunctionFactoryOptions
        {
            Name = "propose_profile_edit",
            Description = "Plan to change the reader's profile: replace one fragment with another (copied exactly from get_profile, appearing once); empty old_text adds new_text at the end; empty new_text removes the fragment. Only for facts the reader stated themselves. A profile change is its own plan, not mixed with other changes.",
        });
}
