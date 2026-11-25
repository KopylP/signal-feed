using PostAgent.Login;

namespace PostAgent.Tests.Login;

public class LoginInputTests
{
    private static readonly string A = Convert.ToBase64String(new byte[PasswordProof.ALength]);
    private static readonly string M1 = Convert.ToBase64String(new byte[PasswordProof.M1Length]);

    [Theory]
    [InlineData(null, "12345", null, "12345")]
    [InlineData(null, " 12 345 ", null, "12345")]
    [InlineData("+380 (67) 123-45-67", null, "+380671234567", null)]
    [InlineData("380671234567", null, "+380671234567", null)]
    public void Accepts_a_phone_or_a_code(string? phone, string? code, string? expectedPhone, string? expectedCode)
    {
        Assert.Equal(new LoginSubmission(expectedPhone, expectedCode, null), LoginInput.Normalize(phone, code, null, null));
    }

    [Fact]
    public void Accepts_a_password_proof_of_the_right_size()
    {
        var submission = LoginInput.Normalize(null, null, A, M1);

        Assert.NotNull(submission?.Proof);
        Assert.Equal(PasswordProof.ALength, submission.Proof.A.Length);
        Assert.Equal(PasswordProof.M1Length, submission.Proof.M1.Length);
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
        Assert.Null(LoginInput.Normalize(phone, code, null, null));
    }

    [Theory]
    [InlineData("AAAA", "x")]
    [InlineData("not base64!", "AAAA")]
    [InlineData(null, "AAAA")]
    public void Rejects_a_proof_that_is_malformed_or_of_the_wrong_size(string? a, string? m1)
    {
        Assert.Null(LoginInput.Normalize(null, null, a, m1));
        Assert.Null(LoginInput.Normalize(null, null, A, "AAAA"));
    }

    [Fact]
    public void Masks_all_but_the_country_code_and_the_last_two_digits()
    {
        Assert.Equal("+380•••••••67", LoginInput.MaskPhone("+380671234567"));
    }
}
