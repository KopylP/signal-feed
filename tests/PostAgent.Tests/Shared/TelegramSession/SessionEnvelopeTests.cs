using System.Security.Cryptography;
using System.Text;
using PostAgent.Shared.TelegramSession;

namespace PostAgent.Tests.Shared.TelegramSession;

public class SessionEnvelopeTests
{
    private static readonly WrappedKey Wrapped = new("https://kv-test.vault.azure.net/keys/tg-session/v1", [1, 2, 3, 4]);
    private static readonly byte[] Plaintext = Encoding.UTF8.GetBytes("auth key of the reader account");

    [Fact]
    public void Opens_what_it_sealed_and_shows_no_plaintext()
    {
        var key = SessionEnvelope.NewDataKey();

        var sealedBlob = SessionEnvelope.Seal(key, Wrapped, Plaintext, "wtelegram.session");

        Assert.True(SessionEnvelope.IsSealed(sealedBlob));
        Assert.Equal(-1, sealedBlob.AsSpan().IndexOf(Plaintext));
        Assert.Equal(Wrapped.KeyId, SessionEnvelope.ReadKey(sealedBlob).KeyId);
        Assert.Equal(Wrapped.Key, SessionEnvelope.ReadKey(sealedBlob).Key);
        Assert.Equal(Plaintext, SessionEnvelope.Open(key, sealedBlob, "wtelegram.session"));
    }

    [Fact]
    public void Uses_a_fresh_nonce_for_every_blob()
    {
        var key = SessionEnvelope.NewDataKey();

        Assert.NotEqual(SessionEnvelope.Seal(key, Wrapped, Plaintext, "p"), SessionEnvelope.Seal(key, Wrapped, Plaintext, "p"));
    }

    [Fact]
    public void Refuses_a_blob_opened_under_another_name()
    {
        var key = SessionEnvelope.NewDataKey();
        var sealedBlob = SessionEnvelope.Seal(key, Wrapped, Plaintext, "login-attempt.json");

        Assert.ThrowsAny<CryptographicException>(() => SessionEnvelope.Open(key, sealedBlob, "wtelegram.session"));
    }

    [Fact]
    public void Refuses_a_blob_whose_key_id_was_changed()
    {
        var key = SessionEnvelope.NewDataKey();
        var sealedBlob = SessionEnvelope.Seal(key, Wrapped, Plaintext, "p");
        var at = sealedBlob.AsSpan().IndexOf("v1"u8);
        sealedBlob[at + 1] = (byte)'2';

        Assert.ThrowsAny<CryptographicException>(() => SessionEnvelope.Open(key, sealedBlob, "p"));
    }

    [Fact]
    public void Refuses_a_changed_ciphertext()
    {
        var key = SessionEnvelope.NewDataKey();
        var sealedBlob = SessionEnvelope.Seal(key, Wrapped, Plaintext, "p");
        sealedBlob[^1] ^= 1;

        Assert.ThrowsAny<CryptographicException>(() => SessionEnvelope.Open(key, sealedBlob, "p"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("SFE1")]
    [InlineData("plain session")]
    public void Refuses_data_that_is_not_a_whole_envelope(string data)
    {
        Assert.ThrowsAny<CryptographicException>(() => SessionEnvelope.Open(SessionEnvelope.NewDataKey(), Encoding.UTF8.GetBytes(data), "p"));
    }
}
