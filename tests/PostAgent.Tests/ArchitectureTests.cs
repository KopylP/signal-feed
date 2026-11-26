using System.Reflection;
using System.Text.RegularExpressions;

namespace PostAgent.Tests;

// Dependency rules from CLAUDE.md that the compiler does not enforce (the rule that Core has no Functions host reference, and that the
// [Function] classes are only in the host, is checked here too because a NuGet package can flow in transitively).
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

    [Fact]
    public void Core_has_no_reference_to_the_azure_functions_worker()
    {
        var core = typeof(PostAgent.ServiceCollectionExtensions).Assembly;

        var references = core.GetReferencedAssemblies().Select(a => a.Name).Where(n => n is not null && n.StartsWith("Microsoft.Azure.Functions", StringComparison.Ordinal)).ToList();

        Assert.True(references.Count == 0, "PostAgent.Core must not depend on the Functions host, found: " + string.Join(", ", references));
    }

    [Fact]
    public void Every_function_class_is_in_the_host_and_none_in_core()
    {
        var host = typeof(PostAgent.Chat.ChatFunction).Assembly;
        var core = typeof(PostAgent.ServiceCollectionExtensions).Assembly;
        Assert.NotEqual(core, host);

        static bool IsFunction(Type type) => type.GetMethods().Any(m => m.GetCustomAttributes().Any(a => a.GetType().FullName == "Microsoft.Azure.Functions.Worker.FunctionAttribute"));

        Assert.Empty(core.GetTypes().Where(IsFunction).Select(t => t.FullName));
        Assert.Contains(host.GetTypes(), IsFunction);
    }

    private static string FindSourceRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "PostAgent.Core");
            if (Directory.Exists(Path.Combine(candidate, "Shared")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("src/PostAgent.Core was not found above the test output folder.");
    }
}
