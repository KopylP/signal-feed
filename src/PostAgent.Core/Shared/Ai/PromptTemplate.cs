using System.Text.RegularExpressions;

namespace PostAgent.Shared.Ai;

// A prompt file with editable sections:
//   <!-- editable: id | label -->
//   text
//   <!-- /editable -->
// The markers are removed when the prompt is rendered; an override replaces the text between them.
public static partial class PromptTemplate
{
    [GeneratedRegex(@"<!--\s*editable:\s*(?<id>[a-z0-9-]+)\s*\|\s*(?<label>[^>]*?)\s*-->\r?\n(?<body>.*?)\r?\n<!--\s*/editable\s*-->\r?\n?", RegexOptions.Singleline)]
    private static partial Regex Marker();

    public static IReadOnlyList<PromptSection> Sections(string file, string text) =>
        Marker().Matches(text)
            .Select(m => new PromptSection(m.Groups["id"].Value, m.Groups["label"].Value, file, m.Groups["body"].Value.Trim()))
            .ToList();

    // overrides: section id to the replacement text. A missing id keeps the default from the file.
    public static string Render(string text, IReadOnlyDictionary<string, string>? overrides) =>
        Marker().Replace(text, m =>
            (overrides is not null && overrides.TryGetValue(m.Groups["id"].Value, out var replacement) ? replacement.Trim() : m.Groups["body"].Value.Trim()) + "\n");

    // The prompt for the agent to read: the same text, with every editable section named.
    public static string Describe(string text, IReadOnlyDictionary<string, string>? overrides) =>
        Marker().Replace(text, m =>
        {
            var body = overrides is not null && overrides.TryGetValue(m.Groups["id"].Value, out var replacement) ? replacement.Trim() : m.Groups["body"].Value.Trim();
            return $"[editable section {m.Groups["id"].Value}: {m.Groups["label"].Value}]\n{body}\n[end of section]\n";
        });
}
