using System.ComponentModel;
using Microsoft.Extensions.AI;
using PostAgent.Shared.Ai;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Infrastructure.Tools;

public sealed class ProposePromptResetTool(PromptLibrary prompts) : IChatTool
{
    public AIFunction Build(ProposalPlan plan) => AIFunctionFactory.Create(
        ([Description("The section id.")] string section, CancellationToken ct) => RunAsync(plan, section, ct),
        new AIFunctionFactoryOptions { Name = "propose_prompt_reset", Description = "Plan to put one editable prompt section back to its default text." });

    private Task<string> RunAsync(ProposalPlan plan, string section, CancellationToken ct) =>
        prompts.FindSection(section) is { } found
            ? plan.AddAsync(new SettingChange(SettingChangeKind.ResetPrompt, found.Id), ct)
            : Task.FromResult("Unknown section. Call get_prompt and use an id of an editable section.");
}
