using System.ComponentModel;
using Microsoft.Extensions.AI;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Agent.Tools;

// Not invasive (ADR-0021): it only stops alerts the reader asked for, so it is done at once.
public sealed class RemoveWatchTool(SettingsEditor editor) : IChatTool
{
    public AIFunction Build(ChatTurn turn) => AIFunctionFactory.Create(
        ([Description("The followed topic exactly as get_settings lists it.")] string phrase, CancellationToken ct) => RunAsync(phrase, ct),
        new AIFunctionFactoryOptions
        {
            Name = "remove_watch",
            Description = "Stop following a topic now, no confirmation needed. The phrase must be one of the followed topics from get_settings.",
        });

    // A made-up phrase is sent back to the list instead of removing something else.
    private async Task<string> RunAsync(string phrase, CancellationToken ct)
    {
        var normalized = WatchPhrases.Normalize(phrase);
        var current = await editor.CurrentWatchesAsync(ct);
        if (normalized is null || current.FirstOrDefault(w => w.Equals(normalized, StringComparison.OrdinalIgnoreCase)) is not { } existing)
        {
            return "That topic is not followed. Call get_settings and ask the reader which one they mean.";
        }

        var result = await editor.RemoveWatchAsync(existing, ct);
        return result.Ok ? "Done: the topic is no longer followed. Tell the reader." : "Not done: " + result.Error;
    }
}
