using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PostAgent.Login;

// Checks the initData that Telegram gives a Mini App, as the Bot API documents it ("Validating data received via the Mini App"):
// the hash is an HMAC-SHA256 of the sorted fields, keyed with HMAC-SHA256("WebAppData", bot token). Only Telegram and we know the bot token.
public static class WebAppInitData
{
    public const int MaxChars = 4096;

    // Clock skew between Telegram and us that is still accepted for an auth_date in the future.
    private static readonly TimeSpan FutureSkew = TimeSpan.FromMinutes(1);

    // The id of the Telegram user who opened the Mini App, or null when the data is not signed by Telegram for this bot or is too old.
    public static long? TryGetUserId(string? initData, string botToken, DateTimeOffset now, TimeSpan maxAge)
    {
        if (string.IsNullOrEmpty(initData) || initData.Length > MaxChars || string.IsNullOrEmpty(botToken))
        {
            return null;
        }

        var fields = Parse(initData);
        if (fields is null || !fields.Remove("hash", out var hash) || !HashMatches(fields, hash, botToken))
        {
            return null;
        }

        if (!fields.TryGetValue("auth_date", out var authDateText)
            || !long.TryParse(authDateText, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
        {
            return null;
        }

        var authDate = DateTimeOffset.FromUnixTimeSeconds(Math.Min(seconds, DateTimeOffset.MaxValue.ToUnixTimeSeconds()));
        if (now - authDate > maxAge || authDate - now > FutureSkew)
        {
            return null;
        }

        return fields.TryGetValue("user", out var user) ? UserId(user) : null;
    }

    private static Dictionary<string, string>? Parse(string initData)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in initData.Split('&'))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2)
            {
                return null;
            }

            // A repeated key would make "which value was signed" ambiguous.
            if (!fields.TryAdd(WebUtility.UrlDecode(parts[0]), WebUtility.UrlDecode(parts[1])))
            {
                return null;
            }
        }

        return fields;
    }

    private static bool HashMatches(Dictionary<string, string> fields, string hash, string botToken)
    {
        if (hash.Length != 64)
        {
            return false;
        }

        byte[] expected;
        try
        {
            expected = Convert.FromHexString(hash);
        }
        catch (FormatException)
        {
            return false;
        }

        var dataCheck = string.Join('\n', fields.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => $"{f.Key}={f.Value}"));
        var secret = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(botToken));
        var actual = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheck));
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static long? UserId(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty("id", out var id)
                   && id.TryGetInt64(out var value) && value > 0
                ? value
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
