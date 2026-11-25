using System.Text.Json;
using Microsoft.Extensions.AI;

namespace PostAgent.Tests.Chat.Agent.Tools;

// The tools of one question, called the way the model calls them: by name, with named arguments, through the real parameter binding.
public sealed class ToolShim(IReadOnlyList<AIFunction> tools)
{
    public Task<string> GetPost(string id, CancellationToken ct) => Call("get_post", ct, ("id", id));

    public Task<string> SearchPosts(string query, int days, CancellationToken ct) => Call("search_posts", ct, ("query", query), ("days", days));

    public Task<string> GetSettings(CancellationToken ct) => Call("get_settings", ct);

    public Task<string> ProposeSet(string key, string value, CancellationToken ct) => Call("propose_set_setting", ct, ("key", key), ("value", value));

    public Task<string> ProposeReset(string key, CancellationToken ct) => Call("propose_reset_setting", ct, ("key", key));

    public Task<string> ProposeResetGroup(string group, CancellationToken ct) => Call("propose_reset_group", ct, ("group", group));

    public Task<string> ProposeAddSource(string name, CancellationToken ct) => Call("propose_add_source", ct, ("name", name));

    public Task<string> ProposeRemoveSource(string name, CancellationToken ct) => Call("propose_remove_source", ct, ("name", name));

    public Task<string> ProposeToggleAlertSource(string name, CancellationToken ct) => Call("propose_toggle_alert_source", ct, ("name", name));

    public Task<string> ProposeClearContext(CancellationToken ct) => Call("propose_clear_context", ct);

    public Task<string> GetPrompt(string name, CancellationToken ct) => Call("get_prompt", ct, ("name", name));

    public Task<string> ProposePromptEdit(string section, string oldText, string newText, CancellationToken ct) =>
        Call("propose_prompt_edit", ct, ("section", section), ("old_text", oldText), ("new_text", newText));

    public Task<string> ProposePromptReset(string section, CancellationToken ct) => Call("propose_prompt_reset", ct, ("section", section));

    public Task<string> GetProfile(CancellationToken ct) => Call("get_profile", ct);

    public Task<string> ProposeProfileEdit(string oldText, string newText, CancellationToken ct) =>
        Call("propose_profile_edit", ct, ("old_text", oldText), ("new_text", newText));

    private async Task<string> Call(string name, CancellationToken ct, params (string Key, object? Value)[] args)
    {
        var tool = tools.Single(t => t.Name == name);
        var result = await tool.InvokeAsync(new AIFunctionArguments(args.ToDictionary(a => a.Key, a => a.Value)), ct);
        return result is JsonElement { ValueKind: JsonValueKind.String } text ? text.GetString()! : result?.ToString() ?? string.Empty;
    }
}
