using System.ComponentModel;
using Microsoft.Extensions.AI;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Agent.Tools;

// Not invasive (ADR-0021): following a topic only adds alerts and is undone by remove_watch, so it is done at once.
public sealed class AddWatchTool(SettingsEditor editor) : IChatTool
{
    public AIFunction Build(ChatTurn turn) => AIFunctionFactory.Create(
        ([Description("The topic to follow, a short phrase like: google core update. A post matches when it is about it, or has every word of it.")] string phrase, CancellationToken ct) => RunAsync(phrase, ct),
        new AIFunctionFactoryOptions
        {
            Name = "add_watch",
            Description = "Follow a topic now, no confirmation needed: from then on a new scored post from any source that is about the topic (by meaning, or with every word of the phrase) is sent as its own alert, whatever its score (not during quiet hours); matches of the last day are announced too. Use it when the reader asks to watch, follow or track a topic.",
        });

    private async Task<string> RunAsync(string phrase, CancellationToken ct)
    {
        if (WatchPhrases.Normalize(phrase) is not { } normalized)
        {
            return $"This is not a usable topic: it must be a short text of at most {WatchPhrases.MaxChars} characters.";
        }

        var result = await editor.AddWatchAsync(normalized, ct);
        return result.Ok ? "Done: the topic is followed now. Tell the reader." : "Not done: " + result.Error;
    }
}
