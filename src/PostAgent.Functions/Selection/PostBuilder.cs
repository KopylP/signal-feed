namespace PostAgent.Selection;

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
        var items = ToTextItems(messages);

        if (source.Type == SourceType.Channel || items.Count <= options.MaxItemsPerGroup)
        {
            return items.Select(item => ToPost(source, item, options)).OfType<PostEntity>().ToList();
        }

        var chunkSize = (int)Math.Ceiling(items.Count / (double)options.MaxItemsPerGroup);
        return items
            .Chunk(chunkSize)
            .Select(chunk => chunk.Length == 1 ? ToPost(source, chunk[0], options) : ToBundle(source, chunk, options))
            .OfType<PostEntity>()
            .ToList();
    }

    private static string MessageUrl(string username, int id) => $"https://t.me/{username}/{id}";

    private static List<Item> ToTextItems(IEnumerable<TgMessage> messages) =>
        messages
            .Where(m => !m.IsService && !string.IsNullOrWhiteSpace(m.Text))
            .OrderBy(m => m.Id)
            .Select(m => new Item(m, m.Text!.Trim()))
            .ToList();

    /// <summary>Окремий пост. Повертає null для занадто короткого повідомлення.</summary>
    private static PostEntity? ToPost(SourceInfo source, Item item, AgentOptions options)
    {
        if (item.Text.Length < options.MinPostChars)
        {
            return null;
        }

        var message = item.Message;
        return new PostEntity
        {
            PartitionKey = PostEntity.PartitionFor(message.Date),
            RowKey = $"{source.Username}_{message.Id}",
            Source = source.Username,
            SourceType = source.Type,
            PostId = message.Id,
            Author = source.Type == SourceType.Group ? message.Author : null,
            Url = MessageUrl(source.Username, message.Id),
            Text = item.Text.Truncate(options.MaxPostChars),
            PublishedAt = message.Date,
        };
    }

    private static PostEntity ToBundle(SourceInfo source, Item[] chunk, AgentOptions options)
    {
        var ranked = chunk
            .OrderByDescending(i => i.Message.Engagement)
            .ThenByDescending(i => i.Text.Length)
            .ToList();

        var first = chunk[0].Message;
        var last = chunk[^1].Message;
        return new PostEntity
        {
            PartitionKey = PostEntity.PartitionFor(last.Date),
            RowKey = $"{source.Username}_{first.Id}-{last.Id}",
            Source = source.Username,
            SourceType = source.Type,
            PostId = last.Id,
            Url = MessageUrl(source.Username, ranked[0].Message.Id),
            Text = BundleText(ranked, options),
            PublishedAt = last.Date,
            MessageCount = chunk.Length,
        };
    }

    /// <summary>Рядки «автор: текст», найпомітніші першими, у межах MaxBundleChars.</summary>
    private static string BundleText(IEnumerable<Item> rankedItems, AgentOptions options)
    {
        var lines = new List<string>();
        var length = 0;

        foreach (var item in rankedItems)
        {
            var line = BundleLine(item, options);
            if (lines.Count > 0 && length + line.Length + 1 > options.MaxBundleChars)
            {
                break;
            }

            lines.Add(line);
            length += line.Length + 1;
        }

        return string.Join('\n', lines);
    }

    private static string BundleLine(Item item, AgentOptions options)
    {
        var author = item.Message.Author is null ? "" : item.Message.Author + ": ";
        return (author + item.Text).Truncate(Math.Min(options.MaxPostChars, options.MaxBundleChars));
    }
}
