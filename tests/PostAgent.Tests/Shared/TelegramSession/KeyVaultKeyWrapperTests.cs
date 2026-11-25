using Azure.Identity;
using PostAgent.Shared.TelegramSession;

namespace PostAgent.Tests.Shared.TelegramSession;

public class KeyVaultKeyWrapperTests
{
    private const string KeyId = "https://kv-signal.vault.azure.net/keys/tg-session";

    // A credential that is never used: every case below must fail before any call to Key Vault.
    private static KeyVaultKeyWrapper Wrapper() => new(KeyId, new ClientSecretCredential("00000000-0000-0000-0000-000000000000", "client", "secret"));

    [Theory]
    [InlineData("https://evil.vault.azure.net/keys/tg-session/v1")]
    [InlineData("https://kv-signal.vault.azure.net/keys/other/v1")]
    [InlineData("https://kv-signal.vault.azure.net/keys/tg-session/v1/../../other/v1")]
    [InlineData("https://kv-signal.vault.azure.net/keys/tg-session-2/v1")]
    public async Task Never_unwraps_with_a_key_named_by_the_blob_that_is_not_ours(string keyId)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Wrapper().UnwrapAsync(new WrappedKey(keyId, [1]), CancellationToken.None));
    }

    [Theory]
    [InlineData("http://kv-signal.vault.azure.net/keys/tg-session")]
    [InlineData("https://kv-signal.vault.azure.net/keys/tg-session/v1")]
    [InlineData("https://kv-signal.vault.azure.net/secrets/tg-session")]
    public void Takes_only_the_versionless_id_of_a_key(string keyId)
    {
        Assert.Throws<ArgumentException>(() => new KeyVaultKeyWrapper(keyId, new ClientSecretCredential("00000000-0000-0000-0000-000000000000", "client", "secret")));
    }
}
