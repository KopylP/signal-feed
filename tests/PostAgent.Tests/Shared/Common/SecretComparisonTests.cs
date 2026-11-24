using PostAgent.Shared.Common;

namespace PostAgent.Tests.Shared.Common;

public class SecretComparisonTests
{
    [Fact]
    public void Equal_secrets_match()
    {
        Assert.True(SecretComparison.Matches("correct-horse", "correct-horse"));
    }

    [Theory]
    [InlineData("wrong")]
    [InlineData("correct-horse ")]
    [InlineData("CORRECT-HORSE")]
    [InlineData("")]
    [InlineData(null)]
    public void Different_or_missing_values_do_not_match(string? actual)
    {
        Assert.False(SecretComparison.Matches("correct-horse", actual));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void An_unset_secret_never_matches_anything(string? actual)
    {
        Assert.False(SecretComparison.Matches("", actual));
    }
}
