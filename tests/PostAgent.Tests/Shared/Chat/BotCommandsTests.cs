using PostAgent.Shared.Chat;

namespace PostAgent.Tests.Shared.Chat;

public class BotCommandsTests
{
    [Fact]
    public void Every_command_starts_with_a_slash_and_is_unique_and_described()
    {
        var names = BotCommands.All.Select(c => c.Usage.Split(' ')[0]).ToList();

        Assert.All(names, n => Assert.StartsWith("/", n));
        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(BotCommands.All, c => Assert.False(string.IsNullOrWhiteSpace(c.Purpose)));
    }

    // The code is the source of the truth: every command literal in the source (`"/word"` as a whole string or a constant, as the handlers
    // compare them) must be in the list the agent gives, so a new handler with a new command cannot be forgotten.
    [Fact]
    public void Every_command_the_code_reads_is_in_the_list()
    {
        var root = Path.Combine(FindRepositoryRoot(), "src");
        var found = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(p => System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(p), "\"(/[a-z]+)\"").Select(m => m.Groups[1].Value))
            .Where(c => c != "/")
            .ToHashSet();

        var listed = BotCommands.All.Select(c => c.Usage.Split(' ')[0]).ToHashSet();

        Assert.Empty(found.Where(c => !listed.Contains(c)).Order());
        Assert.Empty(listed.Where(c => !found.Contains(c)).Order());
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SignalFeed.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
