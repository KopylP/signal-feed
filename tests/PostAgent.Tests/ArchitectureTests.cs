using System.Text.RegularExpressions;

namespace PostAgent.Tests;

// Dependency rules from CLAUDE.md that the compiler does not enforce.
public class ArchitectureTests
{
    private static readonly string SourceRoot = FindSourceRoot();

    [Fact]
    public void Shared_does_not_reference_a_feature()
    {
        var features = Directory.GetDirectories(SourceRoot)
            .Select(Path.GetFileName)
            .Where(name => name is not ("Shared" or "bin" or "obj"))
            .ToList();
        Assert.NotEmpty(features);

        var reference = new Regex($@"\bPostAgent\.({string.Join('|', features)})\b");
        var violations = Directory.EnumerateFiles(Path.Combine(SourceRoot, "Shared"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(file => File.ReadLines(file)
                .Select((line, index) => (file, line, number: index + 1))
                .Where(entry => reference.IsMatch(entry.line)))
            .Select(entry => $"{Path.GetRelativePath(SourceRoot, entry.file)}:{entry.number}: {entry.line.Trim()}")
            .ToList();

        Assert.True(violations.Count == 0, "Shared/ must not depend on a feature:\n" + string.Join('\n', violations));
    }

    private static string FindSourceRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "PostAgent.Functions");
            if (Directory.Exists(Path.Combine(candidate, "Shared")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("src/PostAgent.Functions was not found above the test output folder.");
    }
}
