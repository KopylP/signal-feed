namespace PostAgent.Login;

public static class LoginInput
{
    public const int MaxPasswordChars = 256;
    public const int MaxAttemptIdChars = 64;

    // Telegram login codes are 5 digits today; a little room either way, nothing else.
    public static bool IsCode(string code) => code.Length is >= 4 and <= 8 && code.All(char.IsAsciiDigit);

    // Null when the input is not a plausible submission. Spaces in the code are dropped: people copy "12 345".
    public static LoginSubmission? Normalize(string? code, string? password)
    {
        var digits = string.IsNullOrWhiteSpace(code) ? null : new string(code.Where(c => !char.IsWhiteSpace(c)).ToArray());
        var secret = string.IsNullOrEmpty(password) ? null : password;

        if (digits is null && secret is null)
        {
            return null;
        }

        if ((digits is not null && !IsCode(digits)) || secret?.Length > MaxPasswordChars)
        {
            return null;
        }

        return new LoginSubmission(digits, secret);
    }
}
