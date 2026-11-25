using PostAgent.Login;

namespace PostAgent.Tests.Login;

public class LoginInputTests
{
    [Theory]
    [InlineData("12345", null, "12345", null)]
    [InlineData(" 12 345 ", null, "12345", null)]
    [InlineData("", "secret", null, "secret")]
    [InlineData("12345", "secret", "12345", "secret")]
    public void Accepts_a_code_a_password_or_both(string code, string? password, string? expectedCode, string? expectedPassword)
    {
        Assert.Equal(new LoginSubmission(expectedCode, expectedPassword), LoginInput.Normalize(code, password));
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("12a45", null)]
    [InlineData("123", null)]
    [InlineData("123456789", null)]
    [InlineData("１２３４５", null)]
    public void Rejects_anything_that_is_not_a_code(string code, string? password)
    {
        Assert.Null(LoginInput.Normalize(code, password));
    }

    [Fact]
    public void Rejects_a_password_that_is_too_long()
    {
        Assert.Null(LoginInput.Normalize("12345", new string('x', LoginInput.MaxPasswordChars + 1)));
    }
}
