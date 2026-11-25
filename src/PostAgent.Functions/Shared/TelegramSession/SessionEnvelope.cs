using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace PostAgent.Shared.TelegramSession;

// The stored form of an encrypted blob (ADR-0017): "SFE1" | key id | wrapped data key | nonce | tag | AES-256-GCM ciphertext.
// The header and the purpose (the blob name) are authenticated, so a blob cannot be swapped for another or its key id changed.
public static class SessionEnvelope
{
    public const int DataKeyBytes = 32;
    private const int NonceBytes = 12;
    private const int TagBytes = 16;
    private static readonly byte[] Magic = "SFE1"u8.ToArray();

    public static bool IsSealed(ReadOnlySpan<byte> data) => data.StartsWith(Magic);

    public static byte[] NewDataKey() => RandomNumberGenerator.GetBytes(DataKeyBytes);

    public static byte[] Seal(byte[] dataKey, WrappedKey wrapped, byte[] plaintext, string purpose)
    {
        var header = Header(wrapped);
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var tag = new byte[TagBytes];
        var ciphertext = new byte[plaintext.Length];
        using (var aes = new AesGcm(dataKey, TagBytes))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag, Aad(header, purpose));
        }

        return [.. header, .. nonce, .. tag, .. ciphertext];
    }

    // The wrapped key of a sealed blob, so that the caller can unwrap it (or find it in a cache) before Open.
    public static WrappedKey ReadKey(ReadOnlySpan<byte> data)
    {
        Parse(data, out var wrapped, out _);
        return wrapped;
    }

    public static byte[] Open(byte[] dataKey, ReadOnlySpan<byte> data, string purpose)
    {
        Parse(data, out _, out var headerLength);
        var header = data[..headerLength];
        var rest = data[headerLength..];
        if (rest.Length < NonceBytes + TagBytes)
        {
            throw new CryptographicException("The encrypted blob is truncated.");
        }

        var plaintext = new byte[rest.Length - NonceBytes - TagBytes];
        using var aes = new AesGcm(dataKey, TagBytes);
        aes.Decrypt(rest[..NonceBytes], rest[(NonceBytes + TagBytes)..], rest[NonceBytes..(NonceBytes + TagBytes)], plaintext, Aad(header, purpose));
        return plaintext;
    }

    private static byte[] Header(WrappedKey wrapped)
    {
        var keyId = Encoding.UTF8.GetBytes(wrapped.KeyId);
        var header = new byte[Magic.Length + 2 + keyId.Length + 2 + wrapped.Key.Length];
        var span = header.AsSpan();
        Magic.CopyTo(span);
        BinaryPrimitives.WriteUInt16BigEndian(span[Magic.Length..], checked((ushort)keyId.Length));
        keyId.CopyTo(span[(Magic.Length + 2)..]);
        BinaryPrimitives.WriteUInt16BigEndian(span[(Magic.Length + 2 + keyId.Length)..], checked((ushort)wrapped.Key.Length));
        wrapped.Key.CopyTo(span[(Magic.Length + 4 + keyId.Length)..]);
        return header;
    }

    private static void Parse(ReadOnlySpan<byte> data, out WrappedKey wrapped, out int headerLength)
    {
        if (!IsSealed(data) || data.Length < Magic.Length + 2)
        {
            throw new CryptographicException("The blob is not encrypted with SessionKeyId.");
        }

        var at = Magic.Length;
        var keyIdLength = BinaryPrimitives.ReadUInt16BigEndian(data[at..]);
        at += 2;
        if (data.Length < at + keyIdLength + 2)
        {
            throw new CryptographicException("The encrypted blob is truncated.");
        }

        var keyId = Encoding.UTF8.GetString(data.Slice(at, keyIdLength));
        at += keyIdLength;
        var keyLength = BinaryPrimitives.ReadUInt16BigEndian(data[at..]);
        at += 2;
        if (data.Length < at + keyLength)
        {
            throw new CryptographicException("The encrypted blob is truncated.");
        }

        wrapped = new WrappedKey(keyId, data.Slice(at, keyLength).ToArray());
        headerLength = at + keyLength;
    }

    private static byte[] Aad(ReadOnlySpan<byte> header, string purpose) => [.. header, .. Encoding.UTF8.GetBytes(purpose)];
}
