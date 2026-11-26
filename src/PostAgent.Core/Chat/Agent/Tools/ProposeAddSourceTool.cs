using System.ComponentModel;
using Microsoft.Extensions.AI;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Agent.Tools;

public sealed class ProposeAddSourceTool : IChatTool
{
    public AIFunction Build(ChatTurn turn) => AIFunctionFactory.Create(
        ([Description("The username, like @name or t.me/name.")] string name, CancellationToken ct) => RunAsync(turn.Plan, name, ct),
        new AIFunctionFactoryOptions { Name = "propose_add_source", Description = "Plan to add a Telegram channel or group as a source." });

    private static Task<string> RunAsync(ProposalPlan plan, string name, CancellationToken ct) =>
        SourceRules.TryParse(name) is { } parsed
            ? plan.AddAsync(new SettingChange(SettingChangeKind.AddSource, parsed), ct)
            : Task.FromResult("This is not a valid channel name (5-32 letters, digits and underscores).");
}
