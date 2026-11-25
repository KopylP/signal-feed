namespace PostAgent.Shared.TelegramSession;

// A data key in clear (only in memory) together with its wrapped form (stored in every blob it encrypts).
public sealed record SessionDataKey(byte[] Key, WrappedKey Wrapped);
