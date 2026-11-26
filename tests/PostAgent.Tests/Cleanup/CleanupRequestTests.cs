using PostAgent.Shared.Cleanup;

namespace PostAgent.Tests.Cleanup;

public class CleanupRequestTests
{
    [Fact]
    public void A_request_survives_the_queue_round_trip()
    {
        var request = CleanupRequest.For(new DateOnly(2026, 11, 23));

        var parsed = CleanupRequest.Parse(request.ToMessage());

        Assert.Equal(request, parsed);
        Assert.Equal(new DateOnly(2026, 11, 23), parsed?.Day);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"summarizedThrough\":\"yesterday\"}")]
    [InlineData("{\"summarizedThrough\":null}")]
    public void A_message_that_is_not_a_complete_request_is_not_parsed(string message) =>
        Assert.Null(CleanupRequest.Parse(message));
}
