using static PostAgent.Tests.TestData;
using PostAgent.Collecting;
using PostAgent.Shared.Posts;

namespace PostAgent.Tests.Collecting;

public class PostBuilderTests
{
    private static readonly SourceInfo Channel = new("news", SourceType.Channel);
    private static readonly SourceInfo Group = new("chat", SourceType.Group);

    [Fact]
    public void Skips_service_empty_and_short_messages()
    {
        var messages = new[]
        {
            Message(1, service: true),
            MessageWithoutText(2),
            Message(3, text: "   "),
            Message(4, text: "коротко"),
            Message(5),
        };

        var posts = PostBuilder.Build(Channel, messages, Options());

        var post = Assert.Single(posts);
        Assert.Equal("news_5", post.RowKey);
    }

    [Fact]
    public void Message_exactly_MinPostChars_long_is_kept()
    {
        var posts = PostBuilder.Build(Channel, [Message(1, Text(80))], Options(o => o.MinPostChars = 80));
        Assert.Single(posts);
    }

    [Fact]
    public void Truncates_text_to_MaxPostChars()
    {
        var posts = PostBuilder.Build(Channel, [Message(1, Text(3000))], Options(o => o.MaxPostChars = 1500));
        Assert.Equal(1500, posts[0].Text.Length);
    }

    [Fact]
    public void Channel_post_has_url_keys_and_no_author()
    {
        var message = Message(7, author: "someone");

        var post = Assert.Single(PostBuilder.Build(Channel, [message], Options()));

        Assert.Equal("https://t.me/news/7", post.Url);
        Assert.Equal("news_7", post.RowKey);
        Assert.Equal("2026-11-24", post.PartitionKey);
        Assert.Equal(SourceType.Channel, post.SourceType);
        Assert.Equal(1, post.MessageCount);
        Assert.Null(post.Author);
        Assert.Equal(PostStatus.New, post.Status);
    }

    [Fact]
    public void Channel_posts_are_never_bundled()
    {
        var messages = Enumerable.Range(1, 50).Select(i => Message(i));
        var posts = PostBuilder.Build(Channel, messages, Options());
        Assert.Equal(50, posts.Count);
        Assert.All(posts, p => Assert.Equal(1, p.MessageCount));
    }

    [Fact]
    public void Small_group_keeps_messages_separate_and_stores_author()
    {
        var messages = Enumerable.Range(1, 5).Select(i => Message(i, author: "ann"));

        var posts = PostBuilder.Build(Group, messages, Options(o => o.MaxItemsPerGroup = 5));

        Assert.Equal(5, posts.Count);
        Assert.All(posts, p => Assert.Equal("ann", p.Author));
        Assert.All(posts, p => Assert.Equal(1, p.MessageCount));
    }

    [Fact]
    public void Busy_group_is_split_into_at_most_MaxItemsPerGroup_bundles_without_losing_messages()
    {
        var messages = Enumerable.Range(1, 200).Select(i => Message(i));

        var posts = PostBuilder.Build(Group, messages, Options(o => o.MaxItemsPerGroup = 5));

        Assert.Equal(5, posts.Count);
        Assert.All(posts, p => Assert.Equal(40, p.MessageCount));
        Assert.Equal(200, posts.Sum(p => p.MessageCount));
    }

    [Fact]
    public void Bundle_puts_the_most_engaging_message_first_and_links_to_it()
    {
        var messages = new[]
        {
            Message(1, "Звичайне повідомлення про щось нейтральне", author: "bob"),
            Message(2, "Ключова новина, яку всі обговорюють", engagement: 50, author: "ann"),
            Message(3, "Ще одна репліка в чаті", author: "bob"),
        };

        var post = Assert.Single(PostBuilder.Build(Group, messages, Options(o =>
        {
            o.MaxItemsPerGroup = 1;
            o.MinPostChars = 1;
        })));

        Assert.StartsWith("ann: Ключова новина", post.Text);
        Assert.Equal("https://t.me/chat/2", post.Url);
        Assert.Equal("chat_1-3", post.RowKey);
        Assert.Equal(3, post.PostId);
        Assert.Equal(3, post.MessageCount);
    }

    [Fact]
    public void Bundle_text_respects_MaxBundleChars()
    {
        var messages = Enumerable.Range(1, 100).Select(i => Message(i, Text(200)));

        var posts = PostBuilder.Build(Group, messages, Options(o =>
        {
            o.MaxItemsPerGroup = 1;
            o.MaxBundleChars = 1000;
        }));

        Assert.True(posts[0].Text.Length <= 1000);
        Assert.Equal(100, posts[0].MessageCount);
    }

    [Fact]
    public void Bundle_never_exceeds_MaxBundleChars_even_when_MaxPostChars_is_larger()
    {
        var messages = Enumerable.Range(1, 10).Select(i => Message(i, Text(500)));

        var post = Assert.Single(PostBuilder.Build(Group, messages, Options(o =>
        {
            o.MaxItemsPerGroup = 1;
            o.MaxPostChars = 1500;
            o.MaxBundleChars = 300;
        })));

        Assert.True(post.Text.Length <= 300);
    }

    [Fact]
    public void A_single_leftover_message_after_chunking_follows_the_normal_minimum_length_rule()
    {
        var messages = Enumerable.Range(1, 6).Select(i => Message(i)).Append(Message(7, "коротко"));

        var posts = PostBuilder.Build(Group, messages, Options(o => o.MaxItemsPerGroup = 5)); // чанки: 2, 2, 2, 1

        Assert.Equal(3, posts.Count);
        Assert.All(posts, p => Assert.Equal(2, p.MessageCount));
    }
}
