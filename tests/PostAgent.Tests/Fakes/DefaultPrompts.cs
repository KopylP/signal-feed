using PostAgent.Shared.Ai;

namespace PostAgent.Tests.Fakes;

// The prompts as shipped, without any edit of the owner.
public sealed class DefaultPrompts : IPromptProvider
{
    public static readonly PromptLibrary Library = new();

    public Task<string> GetAsync(string file, CancellationToken ct) =>
        Task.FromResult(Library.Render(Path.GetFileNameWithoutExtension(file), null));
}
