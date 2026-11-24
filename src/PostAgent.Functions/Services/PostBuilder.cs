using System.Text;
using PostAgent.Models;

namespace PostAgent.Services;

/// <summary>
/// Перетворює повідомлення джерела на пости (чиста функція, без Telegram і сховища).
/// Канали: кожне повідомлення — окремий пост.
/// Групи: до MaxItemsPerGroup повідомлень лишаються окремими постами,
/// більше — діляться на MaxItemsPerGroup послідовних пачок приблизно рівного розміру.
/// </summary>
public static class PostBuilder
{
    private sealed record Item(TgMessage Message, string Text);

    public static IReadOnlyList<PostEntity> Build(SourceInfo source, IEnumerable<TgMessage> messages, AgentOptions options)
    {
        var items = messages
            .Where(m => !m.IsService && !string.IsNullOrWhiteSpace(m.Text))
            .OrderBy(m => m.Id)
            .Select(m => new Item(m, m.Text!.Trim()))
            .ToList();

        if (source.Type == SourceType.Channel || items.Count <= options.MaxItemsPerGroup)
        {
            return items.Select(i => Single(source, i, options)).OfType<PostEntity>().ToList();
        }

        var chunkSize = (int)Math.Ceiling(items.Count / (double)options.MaxItemsPerGroup);
        return items
            .Chunk(chunkSize)
            .Select(chunk => chunk.Length == 1 ? Single(source, chunk[0], options) : Bundle(source, chunk, options))
            .OfType<PostEntity>()
            .ToList();
    }

    private static PostEntity? Single(SourceInfo source, Item item, AgentOptions options)
    {
        if (item.Text.Length < options.MinPostChars)
        {
            return null;
        }

        var m = item.Message;
        return new PostEntity
        {
            PartitionKey = PostEntity.PartitionFor(m.Date),
            RowKey = $"{source.Username}_{m.Id}",
            Source = source.Username,
            SourceType = source.Type,
            Kind = PostKind.Post,
            PostId = m.Id,
            Author = source.Type == SourceType.Group ? m.Author : null,
            Url = MessageUrl(source.Username, m.Id),
            Text = Truncate(item.Text, options.MaxPostChars),
            PublishedAt = m.Date,
            Engagement = m.Engagement,
        };
    }

    private static PostEntity Bundle(SourceInfo source, Item[] chunk, AgentOptions options)
    {
        // Спершу найпомітніші повідомлення, щоб ключове не обрізалося лімітом.
        var ranked = chunk
            .OrderByDescending(i => i.Message.Engagement)
            .ThenByDescending(i => i.Text.Length)
            .ToList();

        var text = new StringBuilder();
        foreach (var item in ranked)
        {
            var author = item.Message.Author is null ? "" : item.Message.Author + ": ";
            var line = author + Truncate(item.Text, options.MaxPostChars);
            if (text.Length > 0 && text.Length + line.Length + 1 > options.MaxBundleChars)
            {
                break;
            }
            text.AppendLine(line);
        }

        var first = chunk[0].Message;
        var last = chunk[^1].Message;
        return new PostEntity
        {
            PartitionKey = PostEntity.PartitionFor(last.Date),
            RowKey = $"{source.Username}_{first.Id}-{last.Id}",
            Source = source.Username,
            SourceType = source.Type,
            Kind = PostKind.Bundle,
            PostId = last.Id,
            Url = MessageUrl(source.Username, ranked[0].Message.Id),
            Text = text.ToString().TrimEnd(),
            PublishedAt = last.Date,
            MessageCount = chunk.Length,
            Engagement = chunk.Sum(i => i.Message.Engagement),
        };
    }

    public static string MessageUrl(string username, int id) => $"https://t.me/{username}/{id}";

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];
}
