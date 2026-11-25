using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace PostAgent.Tests.Login;

// Builds initData the way Telegram does, so the tests check our validation against the documented algorithm, not against itself.
internal static class InitDataSigner
{
    public const string BotToken = "123456:ABC-DEF";

    public static string Sign(long userId, DateTimeOffset authDate, string botToken = BotToken, Dictionary<string, string>? extra = null)
    {
        var fields = new Dictionary<string, string>
        {
            ["auth_date"] = authDate.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["query_id"] = "AAHdF6IQAAAAAN0XohDhrOrc",
            ["user"] = $$"""{"id":{{userId}},"first_name":"Owner","language_code":"uk"}""",
        };
        foreach (var (key, value) in extra ?? [])
        {
            fields[key] = value;
        }

        var dataCheck = string.Join('\n', fields.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => $"{f.Key}={f.Value}"));
        var secret = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(botToken));
        var hash = Convert.ToHexStringLower(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheck)));

        return string.Join('&', fields.Select(f => $"{WebUtility.UrlEncode(f.Key)}={Uri.EscapeDataString(f.Value)}")) + "&hash=" + hash;
    }
}
