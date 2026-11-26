using System.ComponentModel;
using Microsoft.Extensions.AI;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Agent.Tools;

public sealed class ProposeSetSettingTool : IChatTool
{
    public AIFunction Build(ChatTurn turn) => AIFunctionFactory.Create(
        ([Description("The setting key, like DigestSize.")] string key, [Description("The new value.")] string value, CancellationToken ct) => RunAsync(turn.Plan, key, value, ct),
        new AIFunctionFactoryOptions
        {
            Name = "propose_set_setting",
            Description = "Plan to set one setting to a value. Use the key from get_settings. Integers as digits, a choice as its exact value, quiet hours as 23:00-07:00 or - to turn them off.",
        });

    private static Task<string> RunAsync(ProposalPlan plan, string key, string value, CancellationToken ct)
    {
        var normalized = SettingsCatalog.Find(key) is { Kind: SettingKind.QuietHours } && value.Trim() is "-" or "off"
            ? string.Empty
            : value.Trim();
        return plan.AddAsync(new SettingChange(SettingChangeKind.Set, key.Trim(), normalized), ct);
    }
}
