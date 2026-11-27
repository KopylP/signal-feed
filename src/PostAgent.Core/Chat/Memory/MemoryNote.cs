namespace PostAgent.Chat.Memory;

// One note of the reader memory as it is stored: a Markdown file whose name starts with the day it was last written,
// followed by its keywords (2026-11-27-work-seo-marketplace.md). The text is built by MemoryNoteFormat, never taken whole from the model.
public sealed record MemoryNote(string FileName, string Text)
{
    // The first heading of the note, or its file name when the text was changed by hand and has none.
    public string Title =>
        Text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("# ", StringComparison.Ordinal)) is { } heading
            ? heading[2..].Trim()
            : FileName;
}
