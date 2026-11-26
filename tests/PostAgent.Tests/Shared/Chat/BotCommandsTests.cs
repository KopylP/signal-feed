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

    // The handlers of the bot are the source of the truth: a command a handler reads must be in the list the agent gives.
    [Theory]
    [InlineData("Digest/DigestInbox.cs", "/digest")]
    [InlineData("Stats/StatsInbox.cs", "/stats")]
    [InlineData("Chat/Intake/ClearInbox.cs", "/clear")]
    public void A_command_a_handler_reads_is_in_the_list(string handler, string command)
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "PostAgent.Functions", handler));

        Assert.Contains($"\"{command}\"", source);
        Assert.Contains(BotCommands.All, c => c.Usage.Split(' ')[0] == command);
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
