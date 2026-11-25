using Azure.Identity;

namespace PostAgent.Shared.TelegramSession;

public static class SessionCipherServices
{
    // In Azure (identity-based storage) the key is required: the app does not start rather than store a session in clear.
    public static IServiceCollection AddSessionCipher(this IServiceCollection services, IConfiguration config)
    {
        var keyId = config["SessionKeyId"];
        if (!string.IsNullOrWhiteSpace(keyId))
        {
            return services.AddSingleton<ISessionCipher>(new EnvelopeCipher(new KeyVaultKeyWrapper(keyId, new DefaultAzureCredential())));
        }

        if (!string.IsNullOrEmpty(config["AzureWebJobsStorage"]))
        {
            return services.AddSingleton<ISessionCipher, UnencryptedLocalCipher>();
        }

        throw new InvalidOperationException("Set SessionKeyId: the Telegram session is encrypted with a Key Vault key (ADR-0017).");
    }
}
