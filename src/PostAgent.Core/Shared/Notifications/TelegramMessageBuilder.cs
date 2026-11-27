using System.Text;

namespace PostAgent.Shared.Notifications;

// Packs blocks of HTML into Telegram messages of at most maxChars, never splitting a block.
// A section that continues in the next message repeats its header.
public sealed class TelegramMessageBuilder(int maxChars)
{
    private const string Separator = "\n\n";

    private readonly List<string> _messages = [];
    private readonly StringBuilder _current = new();

    public TelegramMessageBuilder Add(string block)
    {
        if (_current.Length > 0 && _current.Length + Separator.Length + block.Length > maxChars)
        {
            Flush();
        }

        AppendBlock(block);
        return this;
    }

    // Nothing is written for a section without blocks.
    public TelegramMessageBuilder Section(string header, IEnumerable<string> blocks)
    {
        var headerWritten = false;
        var continued = false;
        foreach (var block in blocks)
        {
            var headerCost = headerWritten ? 0 : header.Length + Separator.Length;
            if (_current.Length > 0 && _current.Length + Separator.Length + headerCost + block.Length > maxChars)
            {
                Flush();
                headerWritten = false;
                continued = true;
            }

            if (!headerWritten)
            {
                AppendBlock(continued ? header + " (далі)" : header);
                headerWritten = true;
            }

            AppendBlock(block);
        }

        return this;
    }

    public IReadOnlyList<string> Build()
    {
        Flush();
        return _messages;
    }

    private void AppendBlock(string block)
    {
        if (_current.Length > 0)
        {
            _current.Append(Separator);
        }

        _current.Append(block);
    }

    private void Flush()
    {
        if (_current.Length > 0)
        {
            _messages.Add(_current.ToString());
            _current.Clear();
        }
    }
}
