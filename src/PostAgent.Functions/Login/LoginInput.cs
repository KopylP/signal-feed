namespace PostAgent.Login;

public static class LoginInput
{
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
    public static LoginSubmission? Normalize(string? phone, string? code, string? srpA, string? srpM1)
    {
        var number = string.IsNullOrWhiteSpace(phone) ? null : NormalizePhone(phone);
        var digits = string.IsNullOrWhiteSpace(code) ? null : new string(code.Where(c => !char.IsWhiteSpace(c)).ToArray());

        if ((!string.IsNullOrWhiteSpace(phone) && number is null) || (digits is not null && !IsCode(digits)))
        {
            return null;
        }

        PasswordProof? proof = null;
        if (srpA is not null || srpM1 is not null)
        {
            proof = Proof(srpA, srpM1);
            if (proof is null)
            {
                return null;
            }
        }

        return number is null && digits is null && proof is null ? null : new LoginSubmission(number, digits, proof);
    }

    private static PasswordProof? Proof(string? a, string? m1)
    {
        var aBytes = FromBase64(a);
        var m1Bytes = FromBase64(m1);
        return aBytes?.Length == PasswordProof.ALength && m1Bytes?.Length == PasswordProof.M1Length ? new PasswordProof(aBytes, m1Bytes) : null;
    }

    private static byte[]? FromBase64(string? text)
    {
        // 256 bytes are 344 characters: anything much longer is not a proof.
        if (string.IsNullOrEmpty(text) || text.Length > 400)
        {
            return null;
        }

        var buffer = new byte[300];
        return Convert.TryFromBase64String(text, buffer, out var written) ? buffer[..written] : null;
    }
}
