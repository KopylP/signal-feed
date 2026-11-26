using System.Text.RegularExpressions;

namespace PostAgent.Shared.Settings;

public static class SourceRules
{
    // A public Telegram username: 5-32 letters, digits and underscores (after SourceNames made it lower case).
    private static readonly Regex Username = new("^[a-z0-9_]{5,32}$", RegexOptions.CultureInvariant);

    // Null when the text is not a username, in any of the forms it is typed in: name, @name, t.me/name, https://t.me/name.
    public static string? TryParse(string typed)
    {
        var name = SourceNames.Normalize(typed);
        return Username.IsMatch(name) ? name : null;
    }
}
