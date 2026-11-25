namespace PostAgent.Shared.Ai;

// A part of a prompt file that the owner may change from the bot. Everything outside the markers is fixed in the file.
public sealed record PromptSection(string Id, string Label, string File, string Default);
