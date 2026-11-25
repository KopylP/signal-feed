namespace PostAgent.Shared.Ai;

// The prompt files as shipped, read once, and the editable sections they declare. Names are the file names without ".md".
public sealed class PromptLibrary
{
    public static readonly IReadOnlyList<string> Names = ["scoring", "digest", "chat"];

    // Line endings are normalized: a checkout with CRLF must not stop a multi-line fragment from matching.
    private readonly Dictionary<string, string> _files = Names.ToDictionary(n => n, n => Prompts.Load(n + ".md").Replace("\r\n", "\n", StringComparison.Ordinal));

    public PromptLibrary()
    {
        Sections = Names.SelectMany(n => PromptTemplate.Sections(n + ".md", _files[n])).ToList();

        // A broken marker (a missing closing one, an empty section) would leave markers in the prompt or swallow fixed text: fail at startup, not in production.
        foreach (var name in Names)
        {
            var markers = System.Text.RegularExpressions.Regex.Count(_files[name], "<!--\\s*editable:");
            var found = Sections.Count(s => s.File == name + ".md");
            if (markers != found || Sections.Any(s => s.Default.Contains("<!--", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"The editable sections of prompts/{name}.md are not marked correctly.");
            }
        }
    }

    public IReadOnlyList<PromptSection> Sections { get; }

    public PromptSection? FindSection(string id) => Sections.FirstOrDefault(s => s.Id == id.Trim());

    public string File(string name) => _files[name];

    public bool Has(string name) => _files.ContainsKey(name);

    public string Render(string name, IReadOnlyDictionary<string, string>? overrides) => PromptTemplate.Render(_files[name], overrides);

    public string Describe(string name, IReadOnlyDictionary<string, string>? overrides) => PromptTemplate.Describe(_files[name], overrides);

    // What is in force for a section: the owner's text, or the default from the file.
    public string Text(string id, IReadOnlyDictionary<string, string>? overrides) =>
        overrides is not null && overrides.TryGetValue(id, out var replacement) ? replacement : FindSection(id)?.Default ?? string.Empty;
}
