using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using OpenAI;
using PostAgent.Chat.Agent;
using PostAgent.Shared.Ai;
using PostAgent.Shared.Chat;
using PostAgent.Tests.Fakes;
using static PostAgent.Tests.TestData;

namespace PostAgent.Tests.Chat.Agent;

// The production path: the real OpenAI client in streaming mode over a transport that answers with the events of the Responses API.
// A version mismatch between OpenAI and Microsoft.Extensions.AI.OpenAI made this path throw MissingMethodException while every offline test was green.
public class ChatStreamingRequestTests
{
    private const string Reasoning = """{"type":"reasoning","id":"rs_1","summary":[],"encrypted_content":"ENC-1"}""";
    private const string FunctionCall = """{"type":"function_call","id":"fc_1","call_id":"call_1","name":"get_post","arguments":"{\"id\":\"2026-11-25/missing\"}","status":"completed"}""";

    private static string Fill(string template, params object[] args)
    {
        for (var k = 0; k < args.Length; k++)
        {
            template = template.Replace("~" + k + "~", args[k].ToString());
        }

        return template;
    }

    private static string Event(string type, string json) => "event: " + type + "\ndata: " + json + "\n\n";

    private static string Response(string status, string output) =>
        Fill("""{"id":"resp_1","object":"response","created_at":1,"status":"~0~","model":"gpt-6.1-terra","output":[~1~],"parallel_tool_calls":true,"tool_choice":"auto","tools":[]}""", status, output);

    private static string MessageItem(string id, string text) =>
        Fill("""{"type":"message","id":"~0~","status":"completed","role":"assistant","content":[{"type":"output_text","text":"~1~","annotations":[]}]}""", id, text);

    private static string TextEvents(ref int n, int index, string id, string text)
    {
        var events = new StringBuilder();
        events.Append(Event("response.output_item.added", Fill("""{"type":"response.output_item.added","sequence_number":~0~,"output_index":~1~,"item":{"type":"message","id":"~2~","status":"in_progress","role":"assistant","content":[]}}""", n++, index, id)));
        events.Append(Event("response.content_part.added", Fill("""{"type":"response.content_part.added","sequence_number":~0~,"item_id":"~2~","output_index":~1~,"content_index":0,"part":{"type":"output_text","text":"","annotations":[]}}""", n++, index, id)));
        var words = text.Split(' ');
        for (var w = 0; w < words.Length; w++)
        {
            // Like the real stream: the space comes with the next word, nothing trails.
            var delta = (w > 0 ? " " : string.Empty) + words[w];
            events.Append(Event("response.output_text.delta", Fill("""{"type":"response.output_text.delta","sequence_number":~0~,"item_id":"~2~","output_index":~1~,"content_index":0,"delta":"~3~","logprobs":[]}""", n++, index, id, delta)));
        }

        events.Append(Event("response.output_text.done", Fill("""{"type":"response.output_text.done","sequence_number":~0~,"item_id":"~2~","output_index":~1~,"content_index":0,"text":"~3~","logprobs":[]}""", n++, index, id, text)));
        events.Append(Event("response.content_part.done", Fill("""{"type":"response.content_part.done","sequence_number":~0~,"item_id":"~2~","output_index":~1~,"content_index":0,"part":{"type":"output_text","text":"~3~","annotations":[]}}""", n++, index, id, text)));
        events.Append(Event("response.output_item.done", Fill("""{"type":"response.output_item.done","sequence_number":~0~,"output_index":~1~,"item":~2~}""", n++, index, MessageItem(id, text))));
        return events.ToString();
    }

    // Hidden reasoning, an optional sentence before the tool call, then the call.
    private static string ReasoningAndToolCall(bool preamble)
    {
        var n = 0;
        var events = new StringBuilder();
        events.Append(Event("response.created", Fill("""{"type":"response.created","sequence_number":~0~,"response":~1~}""", n++, Response("in_progress", ""))));
        events.Append(Event("response.output_item.added", Fill("""{"type":"response.output_item.added","sequence_number":~0~,"output_index":0,"item":{"type":"reasoning","id":"rs_1","summary":[]}}""", n++)));
        events.Append(Event("response.output_item.done", Fill("""{"type":"response.output_item.done","sequence_number":~0~,"output_index":0,"item":~1~}""", n++, Reasoning)));
        var index = 1;
        var output = Reasoning;
        if (preamble)
        {
            events.Append(TextEvents(ref n, index, "msg_p", "Зараз гляну."));
            output += "," + MessageItem("msg_p", "Зараз гляну.");
            index++;
        }

        events.Append(Event("response.output_item.added", Fill("""{"type":"response.output_item.added","sequence_number":~0~,"output_index":~1~,"item":{"type":"function_call","id":"fc_1","call_id":"call_1","name":"get_post","arguments":"","status":"in_progress"}}""", n++, index)));
        events.Append(Event("response.function_call_arguments.delta", Fill("""{"type":"response.function_call_arguments.delta","sequence_number":~0~,"item_id":"fc_1","output_index":~1~,"delta":"{\"id\":\"2026-11-25/missing\"}"}""", n++, index)));
        events.Append(Event("response.function_call_arguments.done", Fill("""{"type":"response.function_call_arguments.done","sequence_number":~0~,"item_id":"fc_1","output_index":~1~,"arguments":"{\"id\":\"2026-11-25/missing\"}"}""", n++, index)));
        events.Append(Event("response.output_item.done", Fill("""{"type":"response.output_item.done","sequence_number":~0~,"output_index":~1~,"item":~2~}""", n++, index, FunctionCall)));
        output += "," + FunctionCall;
        events.Append(Event("response.completed", Fill("""{"type":"response.completed","sequence_number":~0~,"response":~1~}""", n++, Response("completed", output))));
        return events.ToString();
    }

    private static string TextAnswer(string text)
    {
        var n = 0;
        var events = new StringBuilder();
        events.Append(Event("response.created", Fill("""{"type":"response.created","sequence_number":~0~,"response":~1~}""", n++, Response("in_progress", ""))));
        events.Append(TextEvents(ref n, 0, "msg_x", text));
        events.Append(Event("response.completed", Fill("""{"type":"response.completed","sequence_number":~0~,"response":~1~}""", n++, Response("completed", MessageItem("msg_x", text)))));
        return events.ToString();
    }

    private static string JsonAnswer(string output) => Response("completed", output);

    private sealed class Handler(bool stream, params string[] answers) : HttpMessageHandler
    {
        private int _next;

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            var body = answers[Math.Min(_next++, answers.Length - 1)];
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, stream ? "text/event-stream" : "application/json") };
        }
    }

    private static async Task<(Handler Handler, RecordingDrafts Drafts, string First, string Second)> RunAsync(bool stream, bool preamble)
    {
        var handler = stream
            ? new Handler(true, ReasoningAndToolCall(preamble), TextAnswer("Перша відповідь"), TextAnswer("Друга відповідь"))
            : new Handler(false,
                JsonAnswer(Reasoning + (preamble ? "," + MessageItem("msg_p", "Зараз гляну.") : string.Empty) + "," + FunctionCall),
                JsonAnswer(MessageItem("msg_x", "Перша відповідь")),
                JsonAnswer(MessageItem("msg_x", "Друга відповідь")));
        var settings = Options(o =>
        {
            o.OpenAIApiKey = "key";
            o.ChatStreamEnabled = stream;
        });
        var chat = AiServices.CreateChatClient(settings, new OpenAIClientOptions { Transport = new HttpClientPipelineTransport(new HttpClient(handler)) });
        var drafts = new RecordingDrafts();
        var streams = new ChatDraftStreams(drafts, new FakeTimeProvider(Now) { AutoAdvanceAmount = TimeSpan.FromSeconds(1) }, NullLogger<ChatDraftStream>.Instance);
        var agent = new ChatAgent(chat, new PostAgent.Tests.SettingsMenu.MenuHarness().ToolsFactory, new DefaultPrompts(), new PostAgent.Tests.SettingsMenu.MenuHarness().ReaderMemory, streams, new FakeEffectiveOptions(settings), NullLogger<ChatAgent>.Instance);
        var state = new ChatState(new ReportSnapshot("r1", Now, "Огляд", [], []), [], null);

        var first = await agent.AskAsync(ChatId, state, "перше", null, CancellationToken.None);
        // The conversation goes through the blob between two questions, like in production.
        Assert.True(ChatStateJson.TryRead(ChatStateJson.Write(state.WithProcessed(1, first.Session)), out var reloaded));
        var second = await agent.AskAsync(ChatId, reloaded, "друге", null, CancellationToken.None);
        return (handler, drafts, first.Text, second.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_hidden_reasoning_and_the_tool_result_are_sent_back_while_streaming(bool preamble)
    {
        var (handler, _, first, second) = await RunAsync(stream: true, preamble);

        Assert.Equal(3, handler.Bodies.Count);
        Assert.All(handler.Bodies, b => Assert.Contains("\"stream\":true", b));
        Assert.Contains("ENC-1", handler.Bodies[1]);
        Assert.Contains("function_call_output", handler.Bodies[1]);
        Assert.Contains("ENC-1", handler.Bodies[2]);
        Assert.Contains("function_call_output", handler.Bodies[2]);
        Assert.EndsWith("Перша відповідь", first);
        Assert.Equal("Друга відповідь", second);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_streamed_answer_is_the_same_text_as_the_one_that_arrives_at_once(bool preamble)
    {
        var streamed = await RunAsync(stream: true, preamble);
        var whole = await RunAsync(stream: false, preamble);

        Assert.Equal(whole.First, streamed.First);
        Assert.Equal(whole.Second, streamed.Second);
        if (preamble)
        {
            Assert.Equal("Зараз гляну.\nПерша відповідь", streamed.First);
        }
    }

    [Fact]
    public async Task The_preview_of_the_first_answer_grows_towards_it_and_keeps_the_join_between_messages()
    {
        var (_, drafts, first, _) = await RunAsync(stream: true, preamble: true);

        var ofFirstAnswer = drafts.Sent.Where(d => d.DraftId == drafts.Sent[0].DraftId).Select(d => d.Text).ToList();
        Assert.Equal(ChatDraftStream.Placeholder, ofFirstAnswer[0]);
        Assert.DoesNotContain(ofFirstAnswer.Where(t => t != ChatDraftStream.Placeholder), t => t.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length < ChatDraftStream.MinWords);
        Assert.Contains(ofFirstAnswer, t => t.StartsWith("Зараз гляну.\nПерша", StringComparison.Ordinal));
        Assert.All(ofFirstAnswer.Where(t => t != ChatDraftStream.Placeholder), t => Assert.StartsWith(t.TrimEnd(), first));
    }
}
