namespace PostAgent.Shared.Ai;

// The prompt files as shipped, read once, and the editable sections they declare. Names are the file names without ".md".
public sealed class PromptLibrary
{
    public static readonly IReadOnlyList<string> Names = ["scoring", "digest", "chat"];

    private readonly Dictionary<string, string> _files = Names.ToDictionary(n => n, n => Prompts.Load(n + ".md"));

    public PromptLibrary()
    {
        Sections = Names.SelectMany(n => PromptTemplate.Sections(n + ".md", _files[n])).ToList();
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
