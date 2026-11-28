using PostAgent.Deduplication;
using PostAgent.Shared.Posts;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Embedding;

public class DuplicateFinderTests
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(3);

    private static PostEntity WithVector(string key, double hoursAgo, float[] vector, int messages = 1, PostStatus status = PostStatus.New)
    {
        var post = Post(key, score: status == PostStatus.New ? null : 5, hoursAgo: hoursAgo, status: status, messages: messages);
        post.Embedding = PostVectors.ToBytes(vector);
        return post;
    }

    [Fact]
    public void Marks_a_new_post_that_reads_like_an_earlier_one_and_takes_it_out_of_scoring()
    {
        var earlier = WithVector("old", hoursAgo: 3, [1, 0], status: PostStatus.Scored);
        var repeat = WithVector("new", hoursAgo: 1, [0.99f, 0.05f]);

        var duplicates = DuplicateFinder.Mark([repeat], [earlier, repeat], threshold: 0.9, Window);

        Assert.Same(repeat, Assert.Single(duplicates));
        Assert.Equal("old", repeat.DuplicateOf);
        Assert.Equal(PostStatus.Scored, repeat.Status);
    }

    [Fact]
    public void Leaves_a_post_that_is_only_on_a_related_subject()
    {
        var earlier = WithVector("old", hoursAgo: 5, [1, 0]);
        var related = WithVector("new", hoursAgo: 1, [0.7f, 0.7f]);

        Assert.Empty(DuplicateFinder.Mark([related], [earlier, related], threshold: 0.9, Window));
        Assert.Null(related.DuplicateOf);
        Assert.Equal(PostStatus.New, related.Status);
    }

    [Fact]
    public void Never_compares_posts_published_further_apart_than_the_window()
    {
        var earlier = WithVector("old", hoursAgo: 5, [1, 0], status: PostStatus.Scored);
        var repeat = WithVector("new", hoursAgo: 1, [1, 0]);

        Assert.Empty(DuplicateFinder.Mark([repeat], [earlier, repeat], threshold: 0.9, Window));
    }

    [Fact]
    public void Never_compares_group_bundles()
    {
        var earlier = WithVector("old", hoursAgo: 5, [1, 0], messages: 4);
        var bundle = WithVector("new", hoursAgo: 1, [1, 0], messages: 4);

        Assert.Empty(DuplicateFinder.Mark([bundle], [earlier, bundle], threshold: 0.9, Window));
    }

    [Fact]
    public void A_stored_post_with_a_vector_is_the_original_even_when_published_later()
    {
        var repost = WithVector("repost", hoursAgo: 1, [1, 0]);
        var original = WithVector("original", hoursAgo: 2, [1, 0]);

        var duplicates = DuplicateFinder.Mark([original], [repost, original], threshold: 0.9, Window);

        Assert.Same(original, Assert.Single(duplicates));
        Assert.Equal("repost", original.DuplicateOf);
    }

    [Fact]
    public void Keeps_the_earlier_of_two_alike_posts_of_one_batch()
    {
        var first = WithVector("a", hoursAgo: 3, [1, 0]);
        var second = WithVector("b", hoursAgo: 2, [1, 0]);
        var third = WithVector("c", hoursAgo: 1, [1, 0]);

        var duplicates = DuplicateFinder.Mark([third, second, first], [first, second, third], threshold: 0.9, Window);

        Assert.Equal(["b", "c"], duplicates.Select(p => p.RowKey));
        Assert.All(duplicates, p => Assert.Equal("a", p.DuplicateOf));
        Assert.Null(first.DuplicateOf);
    }
}
