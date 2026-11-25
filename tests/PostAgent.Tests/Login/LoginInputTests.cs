using PostAgent.Login;

namespace PostAgent.Tests.Login;

public class LoginInputTests
{
    [Theory]
    [InlineData(null, "12345", null, null, "12345", null)]
    [InlineData(null, " 12 345 ", null, null, "12345", null)]
    [InlineData(null, "", "secret", null, null, "secret")]
    [InlineData(null, "12345", "secret", null, "12345", "secret")]
    [InlineData("+380 (67) 123-45-67", null, null, "+380671234567", null, null)]
    [InlineData("380671234567", null, null, "+380671234567", null, null)]
    public void Accepts_a_phone_a_code_a_password_or_several(
        string? phone, string? code, string? password, string? expectedPhone, string? expectedCode, string? expectedPassword)
    {
        Assert.Equal(new LoginSubmission(expectedPhone, expectedCode, expectedPassword), LoginInput.Normalize(phone, code, password));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData(null, "12a45")]
    [InlineData(null, "123")]
    [InlineData(null, "123456789")]
    [InlineData(null, "１２３４５")]
    [InlineData("+38 abc", null)]
    [InlineData("+123", null)]
    [InlineData("+1234567890123456", null)]
    public void Rejects_anything_that_is_not_a_phone_or_a_code(string? phone, string? code)
    {
        Assert.Null(LoginInput.Normalize(phone, code, null));
    }

    [Fact]
    public void Rejects_a_password_that_is_too_long()
    {
        Assert.Null(LoginInput.Normalize(null, "12345", new string('x', LoginInput.MaxPasswordChars + 1)));
    }

    [Theory]
    [InlineData("+380671234567", "380 67 123 45 67", true)]
    [InlineData("+380671234567", "+380671234568", false)]
    [InlineData("", "+380671234567", false)]
    public void Compares_phones_by_their_digits(string a, string b, bool expected)
    {
        Assert.Equal(expected, LoginInput.SamePhone(a, b));
    }

    [Fact]
    public void Masks_all_but_the_country_code_and_the_last_two_digits()
    {
        Assert.Equal("+380•••••••67", LoginInput.MaskPhone("+380671234567"));
    }
}
