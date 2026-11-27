using System.Text.Json;
using Microsoft.Extensions.AI;

namespace PostAgent.Tests.Chat.Agent.Tools;

// The tools of one question, called the way the model calls them: by name, with named arguments, through the real parameter binding.
public sealed class ToolShim(IReadOnlyList<AIFunction> tools)
{
    public Task<string> GetPost(string id, CancellationToken ct) => Call("get_post", ct, ("id", id));

    public Task<string> SearchPosts(string query, int days, CancellationToken ct) => Call("search_posts", ct, ("query", query), ("days", days));

    // The way the model asks by period: only the arguments it names are passed.
    public Task<string> SearchPostsBetween(string? query, string? from, string? to, CancellationToken ct) =>
        Call("search_posts", ct, ("query", query), ("from", from), ("to", to));

    public Task<string> GetTrends(int? days, string? topic, CancellationToken ct) => Call("get_trends", ct, ("days", days), ("topic", topic));

    public Task<string> GetCommands(CancellationToken ct) => Call("get_commands", ct);

    public Task<string> GetSettings(CancellationToken ct) => Call("get_settings", ct);

    public Task<string> ProposeSet(string key, string value, CancellationToken ct) => Call("propose_set_setting", ct, ("key", key), ("value", value));

    public Task<string> ProposeReset(string key, CancellationToken ct) => Call("propose_reset_setting", ct, ("key", key));

    public Task<string> ProposeResetGroup(string group, CancellationToken ct) => Call("propose_reset_group", ct, ("group", group));

    public Task<string> ProposeAddSource(string name, CancellationToken ct) => Call("propose_add_source", ct, ("name", name));

    public Task<string> ProposeRemoveSource(string name, CancellationToken ct) => Call("propose_remove_source", ct, ("name", name));

    public Task<string> AddWatch(string phrase, CancellationToken ct) => Call("add_watch", ct, ("phrase", phrase));

    public Task<string> RemoveWatch(string phrase, CancellationToken ct) => Call("remove_watch", ct, ("phrase", phrase));

    public Task<string> RatePost(string id, bool liked, CancellationToken ct) => Call("rate_post", ct, ("id", id), ("liked", liked));

    public Task<string> ProposeToggleAlertSource(string name, CancellationToken ct) => Call("propose_toggle_alert_source", ct, ("name", name));

    public Task<string> OfferChoices(string[] options, CancellationToken ct) => Call("offer_choices", ct, ("options", options));

    public Task<string> ClearContext(CancellationToken ct) => Call("clear_context", ct);

    public Task<string> GetPrompt(string name, CancellationToken ct) => Call("get_prompt", ct, ("name", name));

    public Task<string> ProposePromptEdit(string section, string oldText, string newText, CancellationToken ct) =>
        Call("propose_prompt_edit", ct, ("section", section), ("old_text", oldText), ("new_text", newText));

    public Task<string> ProposePromptReset(string section, CancellationToken ct) => Call("propose_prompt_reset", ct, ("section", section));

    public Task<string> SaveMemory(string? note, string title, string[] keywords, string[] facts, CancellationToken ct) =>
        Call("save_memory", ct, ("note", note), ("title", title), ("keywords", keywords), ("facts", facts));

    public Task<string> ForgetMemory(string note, CancellationToken ct) => Call("forget_memory", ct, ("note", note));

    private async Task<string> Call(string name, CancellationToken ct, params (string Key, object? Value)[] args)
    {
        var tool = tools.Single(t => t.Name == name);
        var result = await tool.InvokeAsync(new AIFunctionArguments(args.ToDictionary(a => a.Key, a => a.Value)), ct);
        return result is JsonElement { ValueKind: JsonValueKind.String } text ? text.GetString()! : result?.ToString() ?? string.Empty;
    }
}
