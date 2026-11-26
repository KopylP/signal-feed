using System.ComponentModel;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.AI;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Agent.Tools;

public sealed class GetSettingsTool(SettingsEditor editor, IEffectiveOptions effective) : IChatTool
{
    public AIFunction Build(ProposalPlan plan) => AIFunctionFactory.Create(
        (CancellationToken ct) => RunAsync(ct),
        new AIFunctionFactoryOptions
        {
            Name = "get_settings",
            Description = "List the settings the reader can change (key, label, current value, allowed range or choices) and the current sources. Call it before proposing, and to show the reader a list to pick from.",
        });

    private async Task<string> RunAsync(CancellationToken ct)
    {
        var options = await effective.GetFreshAsync(ct);
        var text = new StringBuilder();
        foreach (var group in SettingsCatalog.All.GroupBy(d => d.Group))
        {
            text.Append('[').Append(group.Key).AppendLine("]");
            foreach (var d in group)
            {
                text.Append(d.Key).Append(" (").Append(d.Label).Append("): ").Append(SettingsCatalog.Display(d, SettingsCatalog.Raw(d, options))).Append(Allowed(d)).AppendLine();
            }
        }

        var (sources, alerts) = await editor.CurrentSourcesAsync(ct);
        text.AppendLine("[Sources]");
        foreach (var source in sources)
        {
            text.Append('@').Append(source.Name).Append(alerts.Contains(source.Name) ? " (alerts on)" : " (alerts off)").AppendLine();
        }

        text.AppendLine("[Followed topics]");
        var watches = await editor.CurrentWatchesAsync(ct);
        text.AppendLine(watches.Count == 0 ? "(none)" : string.Join("\n", watches));

        return text.ToString();
    }

    private static string Allowed(SettingDefinition d) => d.Kind switch
    {
        SettingKind.Int => $" [{d.Min.ToString(CultureInfo.InvariantCulture)}..{d.Max.ToString(CultureInfo.InvariantCulture)}]",
        SettingKind.Choice => " [" + string.Join(" | ", d.Choices.Select(c => c.Value)) + "]",
        SettingKind.QuietHours => " [HH:mm-HH:mm or empty]",
        _ => string.Empty,
    };
}
