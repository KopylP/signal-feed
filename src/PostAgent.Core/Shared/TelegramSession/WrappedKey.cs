namespace PostAgent.Shared.TelegramSession;

// A data key wrapped by the Key Vault key: KeyId names the exact key version that can unwrap it.
public sealed record WrappedKey(string KeyId, byte[] Key);
