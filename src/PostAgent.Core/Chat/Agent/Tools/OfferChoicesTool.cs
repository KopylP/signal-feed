using System.ComponentModel;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace PostAgent.Chat.Agent.Tools;

// Not an action at all: the options are shown as buttons in place of the reader's keyboard, and a tap sends the option's text
// as the reader's next message, so the choice reaches the agent like any typed answer and nothing is stored between the turns.
public sealed partial class OfferChoicesTool : IChatTool
{
    public const int MaxOptions = 30;
    public const int MaxOptionChars = 60;

    public AIFunction Build(ChatTurn turn) => AIFunctionFactory.Create(
        ([Description("The options, each the exact text the reader would type, in the order to show them.")] string[] options) => Offer(turn, options),
        new AIFunctionFactoryOptions
        {
            Name = "offer_choices",
            Description = $"Show buttons under your answer, one per option; a tap sends that option's text back as the reader's next message. " +
                $"Use it when the reader has to pick one of known items (which source to remove: call get_settings first and offer the current source names). " +
                $"Still ask the question in your answer. At most {MaxOptions} options of at most {MaxOptionChars} characters; a later call replaces an earlier one.",
        });

    // Whitespace is collapsed, so that the text a tap sends is the text the model gave; an option that would be cut is left out, never shortened,
    // because a shortened source name would come back as a name that does not exist. A "/" option would be taken for a bot command.
    public static IReadOnlyList<string> Clean(IEnumerable<string?> options) =>
        options
            .Select(o => Whitespace().Replace(o ?? string.Empty, " ").Trim())
            .Where(o => o.Length is > 0 and <= MaxOptionChars && !o.StartsWith('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxOptions)
            .ToList();

    private static string Offer(ChatTurn turn, string?[]? options)
    {
        var given = options ?? [];
        var clean = Clean(given);
        if (clean.Count == 0)
        {
            return $"No buttons shown: give 1 to {MaxOptions} options, each a text of at most {MaxOptionChars} characters that does not start with \"/\".";
        }

        turn.OfferChoices(clean);
        var dropped = given.Length - clean.Count;
        return dropped > 0
            ? $"Shown {clean.Count} buttons under your answer; {dropped} options were left out (empty, repeated, too long or over {MaxOptions}). Ask the question in your answer."
            : $"Shown {clean.Count} buttons under your answer. Ask the question in your answer; the reader's tap comes back as their next message.";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
