namespace PostAgent.Ai;

public static class Prompts
{
    public static string Load(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "prompts", fileName));
}
