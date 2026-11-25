
namespace PostAgent.Shared.Infrastructure;

// Every Azure Storage resource the app uses, in one place. Shared by the features and by TgLogin,
// so that Shared/ never has to reference a feature to create the resources.
public static class StorageNames
{
    public const string PostsTable = "Posts";
    public const string CursorsTable = "ChannelState";
    public const string ChatBindingTable = "ChatBinding";
    public const string ScoringQueue = "to-score";
    public const string RunNowQueue = "run-now";
    public const string ChatQueue = "chat-messages";
    public const string ChatStateContainer = "chat-state";
    // One conversation per chat (ADR-0014). The old single blob, "current.json", is only deleted by the cleanup once it is stale.
    public static string ChatStateBlobName(long chatId) => chatId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".json";
    public const string SettingsContainer = "settings";
    public const string SettingsBlob = "overrides.json";

    // What one chat's reader wrote about themselves: a chat never reads another chat's profile.
    public static string ProfileBlobName(long chatId) => $"profile/{chatId.ToString(System.Globalization.CultureInfo.InvariantCulture)}.txt";
    public const string SessionBlob = "wtelegram.session";
    public const string CollectionLockBlob = "collect.lock";
    public const string DefaultSessionContainer = "tg-session";
}
