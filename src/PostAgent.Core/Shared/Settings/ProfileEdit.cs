namespace PostAgent.Shared.Settings;

// The reader profile as a plan's edits would leave it (Text), or why they do not apply (Error), with the version they were checked against.
public sealed record ProfileEdit(string? Text, string? Error, ProfileHandle? Handle)
{
    public static readonly ProfileEdit None = new(null, null, null);
}
