
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
    // /digest: a request for a digest of a period, answered to the chat that asked. Created by the runtime like a poison queue.
    public const string DigestRequestQueue = "digest-requests";
    public const string ChatStateContainer = "chat-state";
    // One conversation per chat (ADR-0014). The old single blob, "current.json", is only deleted by the cleanup once it is stale.
    public static string ChatStateBlobName(long chatId) => chatId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".json";
    public const string SettingsContainer = "settings";
    public const string SettingsBlob = "overrides.json";

    // The reader's 👍 and 👎 on digest entries, shared by every chat; the scoring reads it.
    public const string FeedbackBlob = "feedback.json";

    // What one chat's reader wrote about themselves: a chat never reads another chat's profile.
    public static string ProfileBlobName(long chatId) => $"profile/{chatId.ToString(System.Globalization.CultureInfo.InvariantCulture)}.txt";
    public const string SessionBlob = "wtelegram.session";
    public const string CollectionLockBlob = "collect.lock";
    // The sign-in from the bot that is in progress (ADR-0016); it may hold a login code for a few minutes.
    public const string LoginAttemptBlob = "login-attempt.json";
    public const string LoginQueue = "tg-login";
    public const string DefaultSessionContainer = "tg-session";
}
