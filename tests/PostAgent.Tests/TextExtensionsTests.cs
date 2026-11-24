using PostAgent.Services;

namespace PostAgent.Tests;

public class TextExtensionsTests
{
    [Fact]
    public void Short_text_is_returned_unchanged()
    {
        Assert.Equal("abc", "abc".Truncate(5, "…"));
    }

    [Fact]
    public void Long_text_is_cut_and_gets_the_suffix()
    {
        Assert.Equal("abc…", "abcdef".Truncate(3, "…"));
    }

    [Fact]
    public void Surrogate_pair_is_never_split()
    {
        var text = "ab😀cd"; // 😀 займає дві позиції: індекси 2 і 3

        var result = text.Truncate(3);

        Assert.Equal("ab", result);
    }
}
