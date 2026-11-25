using System.ComponentModel;
using Microsoft.Extensions.AI;
using PostAgent.Shared.Ai;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Infrastructure.Tools;

public sealed class GetPromptTool(PromptLibrary prompts, IOverridesStore overrides) : IChatTool
{
    public AIFunction Build(ProposalPlan plan) => AIFunctionFactory.Create(
        ([Description("scoring, digest or chat.")] string name, CancellationToken ct) => RunAsync(name, ct),
        new AIFunctionFactoryOptions
        {
            Name = "get_prompt",
            Description = "Show a prompt of the agent as it is now. name is scoring (how posts are scored), digest (the daily digest) or chat (this conversation). The parts marked as editable sections can be changed; everything else is fixed.",
        });

    private async Task<string> RunAsync(string name, CancellationToken ct)
    {
        var key = name.Trim().ToLowerInvariant();
        if (!prompts.Has(key))
        {
            return "Unknown prompt. Use one of: " + string.Join(", ", PromptLibrary.Names);
        }

        return prompts.Describe(key, (await overrides.LoadAsync(ct))?.Value.PromptSections);
    }
}
