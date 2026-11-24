using System.Globalization;
using System.Text;
using PostAgent.Shared.Common;
using PostAgent.Shared.Notifications;
using PostAgent.Shared.Posts;

namespace PostAgent.Digest;

public static class DigestMessage
{
    public const string NoPosts = "За добу нових постів не було";

    // Telegram allows 4096 characters. The text is cut per field first, then packed into messages with a margin.
    private const int MaxMessageChars = 3800;
    private const int MaxOverviewChars = 500;
    private const int MaxTitleChars = 100;
    private const int MaxTextChars = 700;
    private const int MaxWhyChars = 350;
    private const int MaxVerificationChars = 250;

    public static IReadOnlyList<string> Format(DigestReport report)
    {
        var writer = new MessageWriter();

        var intro = "📰 <b>Дайджест</b>";
        if (report.Overview is not null)
        {
            intro += "\n\n" + TelegramHtml.Escape(report.Overview.Truncate(MaxOverviewChars, "…"));
        }

        writer.Add(intro);
        writer.Section("🔥 <b>Головне за день</b>", report.Headlines.Select(Entry));
        writer.Section("💼 <b>Корисно для Collaborator</b>", report.ForMarketplace.Select(Entry));
        writer.Section("👀 <b>Варто звернути увагу</b>", report.Notable.Select(Entry));
        writer.Section("📖 <b>Рекомендую прочитати</b>", report.Recommended.Select(Entry));
        writer.Section("⚠️ <b>Потребує перевірки</b>", report.NeedsVerification.Count == 0 ? [] : [Verification(report.NeedsVerification)]);
        writer.Section("📌 <b>Можна пропустити</b>", report.Skipped.Total == 0 ? [] : [SkippedLine(report.Skipped)]);

        return writer.Messages();
    }

    private static string Entry(DigestEntry entry)
    {
        var full = Block(entry, MaxTextChars, MaxWhyChars);
        return full.Length <= MaxMessageChars ? full : Block(entry, 0, 0); // text that grows when escaped: keep only the title and the link
    }

    private static string Block(DigestEntry entry, int maxText, int maxWhy)
    {
        var post = entry.Post;
        var block = new StringBuilder();
        block.Append("<b>").Append(TelegramHtml.Escape((entry.Title ?? post.Topic ?? string.Empty).Truncate(MaxTitleChars, "…"))).Append("</b>");
        block.Append('\n').Append(SourceLine(post));
        if (maxText > 0)
        {
            block.Append('\n').Append(TelegramHtml.Escape(entry.Text.Truncate(maxText, "…")));
        }

        if (maxWhy > 0 && entry.Why is not null)
        {
            block.Append("\n<i>Чому важливо:</i> ").Append(TelegramHtml.Escape(entry.Why.Truncate(maxWhy, "…")));
        }

        block.Append("\n<a href=\"").Append(TelegramHtml.Escape(post.Url)).Append("\">Читати</a>");
        return block.ToString();
    }

    private static string SourceLine(PostEntity post)
    {
        var line = $"@{TelegramHtml.Escape(post.Source)} · {post.PublishedAt.UtcDateTime.ToString("dd.MM", CultureInfo.InvariantCulture)}";
        return post.MessageCount > 1 ? $"{line} · {post.MessageCount} повідомлень" : line;
    }

    // One block: the items are kept while they fit, never cut in the middle of an escaped character.
    private static string Verification(IEnumerable<string> items)
    {
        var block = new StringBuilder();
        foreach (var item in items)
        {
            var line = "• " + TelegramHtml.Escape(item.Truncate(MaxVerificationChars, "…"));
            if (block.Length + line.Length + 1 > MaxMessageChars / 2)
            {
                break;
            }

            block.Append(block.Length > 0 ? "\n" : string.Empty).Append(line);
        }

        return block.ToString();
    }

    private static string SkippedLine(DigestSkipped skipped)
    {
        var parts = new List<string>();
        if (skipped.Duplicates > 0)
        {
            parts.Add($"повторів — {skipped.Duplicates}");
        }

        if (skipped.Other > 0)
        {
            parts.Add($"менш важливих — {skipped.Other}");
        }

        return $"Ще {skipped.Total} {PostWord(skipped.Total)} ({string.Join(", ", parts)}).";
    }

    private static string PostWord(int count) => (count % 100, count % 10) switch
    {
        ( >= 11 and <= 14, _) => "постів",
        (_, 1) => "пост",
        (_, >= 2 and <= 4) => "пости",
        _ => "постів",
    };

    // Packs blocks into messages. A section that continues in the next message repeats its header.
    private sealed class MessageWriter
    {
        private readonly List<string> _messages = [];
        private readonly StringBuilder _current = new();

        public void Add(string block)
        {
            if (_current.Length > 0 && _current.Length + 2 + block.Length > MaxMessageChars)
            {
                Flush();
            }

            AppendBlock(block);
        }

        public void Section(string header, IEnumerable<string> blocks)
        {
            var headerWritten = false;
            var continued = false;
            foreach (var block in blocks)
            {
                var headerCost = headerWritten ? 0 : header.Length + 2;
                if (_current.Length > 0 && _current.Length + 2 + headerCost + block.Length > MaxMessageChars)
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
        }

        public IReadOnlyList<string> Messages()
        {
            Flush();
            return _messages;
        }

        private void AppendBlock(string block)
        {
            if (_current.Length > 0)
            {
                _current.Append("\n\n");
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
}
