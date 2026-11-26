using System.Text.Json;

namespace PostAgent.Shared.Settings;

public static class SettingsOverridesJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Write(SettingsOverrides overrides) => JsonSerializer.Serialize(overrides, Options);

    public static bool TryRead(string json, out SettingsOverrides overrides)
    {
        try
        {
            if (JsonSerializer.Deserialize<SettingsOverrides>(json, Options) is { Values: not null } read)
            {
                overrides = read;
                return true;
            }
        }
        catch (JsonException)
        {
            // Falls through: an unreadable blob is treated as missing by the caller.
        }

        overrides = SettingsOverrides.Empty;
        return false;
    }
}
