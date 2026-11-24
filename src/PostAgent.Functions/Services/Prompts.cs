namespace PostAgent.Services;

/// <summary>Системні промпти лежать у prompts/*.md і копіюються у збірку.</summary>
public static class Prompts
{
    public static string Load(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "prompts", fileName));
}
