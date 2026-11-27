namespace PostAgent.Chat.Memory;

// What a save_memory or forget_memory call did: the answer for the model, and, when something was written, a line for the reader built by code.
public sealed record MemoryEdit(string Answer, string? Notice = null)
{
    public bool Changed => Notice is not null;
}
