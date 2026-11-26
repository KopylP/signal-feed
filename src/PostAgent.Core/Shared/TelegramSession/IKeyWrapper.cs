namespace PostAgent.Shared.TelegramSession;

// Wraps and unwraps data keys with a key that never leaves Key Vault.
public interface IKeyWrapper
{
    Task<WrappedKey> WrapAsync(byte[] dataKey, CancellationToken ct);

    // The session stream flushes synchronously (WTelegram calls Flush), so wrapping has a synchronous form too.
    WrappedKey Wrap(byte[] dataKey);

    Task<byte[]> UnwrapAsync(WrappedKey wrapped, CancellationToken ct);
}
