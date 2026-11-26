namespace PostAgent.Shared.Ai;

// The system prompt of a model call: the file with the owner's edits of its editable sections.
public interface IPromptProvider
{
    // file is the name with the extension, like "scoring.md".
    Task<string> GetAsync(string file, CancellationToken ct);
}
