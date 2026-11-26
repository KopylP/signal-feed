using System.Text;

namespace PostAgent.Shared.Notifications;

// A button under a message. Telegram allows at most 64 bytes of callback data, so a longer value is a bug of the caller.
public sealed record KeyboardButton
{
    public const int MaxDataBytes = 64;

    public KeyboardButton(string text, string data, ButtonStyle style = ButtonStyle.Default)
    {
        if (Encoding.UTF8.GetByteCount(data) > MaxDataBytes)
        {
            throw new ArgumentException($"Callback data is longer than {MaxDataBytes} bytes.", nameof(data));
        }

        Text = text;
        Data = data;
        Style = style;
    }

    public string Text { get; }

    public string Data { get; }

    public ButtonStyle Style { get; }

    // Set for a button that opens a Telegram Mini App instead of sending callback data (only in a private chat).
    public Uri? WebApp { get; private init; }

    public static KeyboardButton OpenWebApp(string text, Uri url, ButtonStyle style = ButtonStyle.Default) =>
        url.Scheme == Uri.UriSchemeHttps
            ? new KeyboardButton(text, string.Empty, style) { WebApp = url }
            : throw new ArgumentException("A Mini App must be served over HTTPS.", nameof(url));
}
