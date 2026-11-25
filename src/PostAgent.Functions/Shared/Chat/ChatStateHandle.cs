namespace PostAgent.Shared.Chat;

// A loaded state with the version it was read at, so that a save can fail when someone else wrote in between.
public sealed record ChatStateHandle(ChatState State, string ETag);
