namespace PostAgent.Shared.Settings;

public static class SourceNames
{
    private static readonly string[] LinkPrefixes = ["https://t.me/", "http://t.me/", "t.me/", "@"];

    public static IReadOnlyList<string> Parse(string? csv) =>
        (csv ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalize)
            .Distinct()
            .ToList();

    public static string Normalize(string value)
    {
        var name = value.Trim();
        foreach (var prefix in LinkPrefixes.Where(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            name = name[prefix.Length..];
        }
        return name.Trim('/').ToLowerInvariant();
    }
}
