using PostAgent.Shared.Settings;

namespace PostAgent.SettingsMenu;

// The menu's own state in the overrides blob: the question it waits an answer to, and the updates it already handled.
public sealed class MenuState(SettingsEditor editor)
{
    private const int MaxHandledIds = 30;

    public Task<EditResult> SetPendingAsync(PendingInput? pending, CancellationToken ct) =>
        editor.UpdateAsync(o => o.Pending == pending ? o : o with { Pending = pending }, validate: false, ct);

    // Only the question of this chat: another chat's menu must not cancel it.
    public Task<EditResult> ClearPendingAsync(long chatId, CancellationToken ct) =>
        editor.UpdateAsync(o => o.Pending is { } p && p.IsFor(chatId) ? o with { Pending = null } : o, validate: false, ct);

    // True when the id was recorded as handled. False for a resent update, or when it could not be recorded.
    // A set of recent ids, not a watermark: Telegram delivers in parallel and may restart the numbering.
    public async Task<bool> TryMarkHandledAsync(long updateId, CancellationToken ct)
    {
        var marked = false;
        var result = await editor.UpdateAsync(
            o =>
            {
                marked = !o.HandledUpdateIds.Contains(updateId);
                return marked ? o with { HandledUpdateIds = o.HandledUpdateIds.Append(updateId).TakeLast(MaxHandledIds).ToList() } : o;
            },
            validate: false,
            ct);

        return result.Ok && marked;
    }
}
