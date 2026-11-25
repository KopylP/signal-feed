using Microsoft.Extensions.AI;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Agent.Tools;

// Forgetting the conversation is an action, so like a settings change it waits for the reader's tap. The reader can also type /clear themselves.
public sealed class ProposeClearContextTool : IChatTool
{
    public AIFunction Build(ProposalPlan plan) => AIFunctionFactory.Create(
        (CancellationToken ct) => plan.AddAsync(new SettingChange(SettingChangeKind.ClearContext, "context"), ct),
        new AIFunctionFactoryOptions
        {
            Name = "propose_clear_context",
            Description = "Plan to clear the conversation context: the earlier questions and answers are forgotten, the digest stays. Only when the reader asks to forget, reset or start over.",
        });
}
