namespace PostAgent.Login;

public static class LoginInput
{
    public const int MaxPasswordChars = 256;
    public const int MaxAttemptIdChars = 64;

    // Separators people type or paste in a phone number.
    private static readonly char[] PhoneSeparators = [' ', '-', '(', ')', '.'];

    // Telegram login codes are 5 digits today; a little room either way, nothing else.
    public static bool IsCode(string code) => code.Length is >= 4 and <= 8 && code.All(char.IsAsciiDigit);

    // An international number (E.164: up to 15 digits) as "+digits", or null.
    public static string? NormalizePhone(string? phone)
    {
        var compact = new string((phone ?? string.Empty).Where(c => !PhoneSeparators.Contains(c)).ToArray());
        var digits = compact.StartsWith('+') ? compact[1..] : compact;
        return digits.Length is >= 7 and <= 15 && digits.All(char.IsAsciiDigit) ? "+" + digits : null;
    }

    // "+380•••••45": enough to recognize the number in a chat message, not enough to read it.
    public static string MaskPhone(string phone) =>
        phone.Length <= 5 ? "•••" : phone[..4] + new string('•', phone.Length - 6) + phone[^2..];

    // Null when the input is not a plausible submission. Spaces in the code are dropped: people copy "12 345".
    public static LoginSubmission? Normalize(string? phone, string? code, string? password)
    {
        var number = string.IsNullOrWhiteSpace(phone) ? null : NormalizePhone(phone);
        var digits = string.IsNullOrWhiteSpace(code) ? null : new string(code.Where(c => !char.IsWhiteSpace(c)).ToArray());
        var secret = string.IsNullOrEmpty(password) ? null : password;

        if (!string.IsNullOrWhiteSpace(phone) && number is null)
        {
            return null;
        }

        if (number is null && digits is null && secret is null)
        {
            return null;
        }

        if ((digits is not null && !IsCode(digits)) || secret?.Length > MaxPasswordChars)
        {
            return null;
        }

        return new LoginSubmission(number, digits, secret);
    }
}
