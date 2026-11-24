namespace PostAgent.Shared.Infrastructure;

// Every Azure Storage resource the app uses, in one place. Shared by the features and by TgLogin,
// so that Shared/ never has to reference a feature to create the resources.
public static class StorageNames
{
    public const string PostsTable = "Posts";
    public const string CursorsTable = "ChannelState";
    public const string ChatBindingTable = "ChatBinding";
    public const string ScoringQueue = "to-score";
    public const string SessionBlob = "wtelegram.session";
    public const string DefaultSessionContainer = "tg-session";
}
