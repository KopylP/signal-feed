using Microsoft.Extensions.AI;
using PostAgent.Chat.Agent;

namespace PostAgent.Tests.Chat.Agent;

public class ChatHistoryReducerTests
{
    private static ChatMessage User(string text) => new(ChatRole.User, text);
    private static ChatMessage Assistant(string text) => new(ChatRole.Assistant, text);
    private static ChatMessage Call(string id) => new(ChatRole.Assistant, [new FunctionCallContent(id, "get_post")]);
    private static ChatMessage Result(string id) => new(ChatRole.Tool, [new FunctionResultContent(id, "ok")]);

    private static async Task<List<ChatMessage>> Reduce(int max, params ChatMessage[] messages) =>
        (await new ChatHistoryReducer(max).ReduceAsync(messages, CancellationToken.None)).ToList();

    [Fact]
    public async Task A_short_history_is_left_alone()
    {
        var reduced = await Reduce(10, User("report"), User("q"), Assistant("a"));

        Assert.Equal(3, reduced.Count);
    }

    [Fact]
    public async Task The_first_message_with_the_digest_is_always_kept()
    {
        var reduced = await Reduce(4, User("report"), User("q1"), Assistant("a1"), User("q2"), Assistant("a2"), User("q3"));

        Assert.Equal("report", reduced[0].Text);
        Assert.Equal(4, reduced.Count);
        Assert.Equal("q3", reduced[^1].Text);
    }

    [Fact]
    public async Task A_tool_result_is_never_kept_without_the_call_that_asked_for_it()
    {
        // Counting alone would keep: report, Result(c1), Assistant("a1"), User("q2").
        var reduced = await Reduce(4, User("report"), User("q1"), Call("c1"), Result("c1"), Assistant("a1"), User("q2"));

        Assert.DoesNotContain(reduced.Skip(1).Take(1), m => m.Role == ChatRole.Tool);
        Assert.Equal("report", reduced[0].Text);
        Assert.Equal("q2", reduced[^1].Text);
        var calls = reduced.SelectMany(m => m.Contents.OfType<FunctionCallContent>()).Select(c => c.CallId).ToHashSet();
        Assert.All(reduced.SelectMany(m => m.Contents.OfType<FunctionResultContent>()), r => Assert.Contains(r.CallId, calls));
    }
}
