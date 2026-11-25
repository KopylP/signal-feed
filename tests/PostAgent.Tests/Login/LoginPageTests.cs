using System.Diagnostics;
using System.Text.RegularExpressions;
using PostAgent.Login;

namespace PostAgent.Tests.Login;

public partial class LoginPageTests
{
    private const string Nonce = "bm9uY2UtZm9yLXRlc3Rz";

    [Fact]
    public void Runs_only_scripts_with_the_nonce_of_the_policy()
    {
        var html = LoginPage.Html(Nonce);

        Assert.Equal(2, Regex.Matches(html, "<script ").Count);
        Assert.Equal(2, Regex.Matches(html, $"<script nonce=\"{Nonce}\"").Count);
        Assert.Contains($"'nonce-{Nonce}'", LoginPage.ContentSecurityPolicy(Nonce), StringComparison.Ordinal);
    }

    [Fact]
    public void Never_puts_the_password_into_a_request()
    {
        var script = InlineScript().Match(LoginPage.Html(Nonce)).Groups[1].Value;

        Assert.Contains("srpProof(challenge, password.value)", script, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"password\s*:", script);
    }

    [Fact]
    public async Task Has_an_inline_script_that_parses()
    {
        var file = Path.Combine(Path.GetTempPath(), $"login-page-{Guid.NewGuid():N}.js");
        await File.WriteAllTextAsync(file, InlineScript().Match(LoginPage.Html(Nonce)).Groups[1].Value);
        try
        {
            var start = new ProcessStartInfo("node") { RedirectStandardError = true };
            start.ArgumentList.Add("--check");
            start.ArgumentList.Add(file);
            using var node = Process.Start(start) ?? throw new InvalidOperationException("Node.js is needed to test the Mini App script.");
            var error = await node.StandardError.ReadToEndAsync();
            await node.WaitForExitAsync();

            Assert.True(node.ExitCode == 0, error);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [GeneratedRegex("<script nonce=\"[^\"]+\">(.*?)</script>", RegexOptions.Singleline)]
    private static partial Regex InlineScript();
}
