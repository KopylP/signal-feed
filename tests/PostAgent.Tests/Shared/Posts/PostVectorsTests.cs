using PostAgent.Shared.Posts;

namespace PostAgent.Tests.Shared.Posts;

public class PostVectorsTests
{
    [Theory]
    [InlineData(new float[] { 1, 0 }, new float[] { 2, 0 }, 1.0)]
    [InlineData(new float[] { 1, 0 }, new float[] { 0, 3 }, 0.0)]
    [InlineData(new float[] { 1, 1 }, new float[] { -1, -1 }, -1.0)]
    public void Similarity_is_the_cosine_of_the_stored_vector_and_the_other(float[] stored, float[] other, double expected) =>
        Assert.Equal(expected, PostVectors.Similarity(PostVectors.ToBytes(stored), other), precision: 6);

    [Fact]
    public void A_missing_vector_or_one_of_another_size_is_not_similar()
    {
        Assert.Equal(0, PostVectors.Similarity(null, [1f, 0f]));
        Assert.Equal(0, PostVectors.Similarity(PostVectors.ToBytes([1f, 0f]), [1f, 0f, 0f]));
        Assert.Equal(0, PostVectors.Similarity(PostVectors.ToBytes([1f, 0f]), (byte[]?)null));
    }
}
