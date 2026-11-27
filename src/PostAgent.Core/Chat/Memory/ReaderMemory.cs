using PostAgent.Shared.Common;
using PostAgent.Shared.Settings;

namespace PostAgent.Chat.Memory;

// What the chat agent keeps about one reader (ADR-0022): up to ten short Markdown notes that the agent writes and removes itself,
// without a confirmation, and that every question of that chat sees. Each change is told to the reader by a line built here.
public sealed class ReaderMemory(IReaderMemoryStore store, IEffectiveOptions options, TimeProvider time, ILogger<ReaderMemory> log)
{
    public async Task<IReadOnlyList<StoredMemoryNote>> ListAsync(long chatId, CancellationToken ct)
    {
        var notes = await store.ListAsync(chatId, ct);
        return notes.Count == 0 && await TryMoveLegacyProfileAsync(chatId, ct) ? await store.ListAsync(chatId, ct) : notes;
    }

    // note empty: a new note. Otherwise the named note is replaced, and its file name takes today's date and the new keywords.
    public async Task<MemoryEdit> RememberAsync(long chatId, string? note, string? title, IReadOnlyList<string>? keywords, IReadOnlyList<string>? facts, CancellationToken ct)
    {
        if (!MemoryNoteFormat.TryBuild(title, keywords, facts, await TodayAsync(ct), out var built, out var error))
        {
            return new MemoryEdit("Not saved: " + error);
        }

        var notes = await ListAsync(chatId, ct);
        var clash = notes.FirstOrDefault(n => n.Note.FileName.Equals(built!.FileName, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(note))
        {
            if (clash is not null)
            {
                return new MemoryEdit($"Not saved: the note {clash.Note.FileName} already has these keywords today. Update it with note={clash.Note.FileName}.");
            }

            if (notes.Count >= MemoryNoteFormat.MaxNotes)
            {
                return new MemoryEdit($"Not saved: the memory is full ({MemoryNoteFormat.MaxNotes} notes). Merge this into an existing note (note=its file name), or forget_memory a note that no longer matters first.");
            }

            return await store.TryCreateAsync(chatId, built!, ct)
                ? new MemoryEdit($"Saved as {built!.FileName}.", $"🧠 Запам'ятав: «{built!.Title}»")
                : new MemoryEdit("Not saved: the memory changed meanwhile. Try again.");
        }

        if (notes.FirstOrDefault(n => MemoryNoteFormat.SameName(n.Note.FileName, note)) is not { } current)
        {
            return new MemoryEdit("There is no such note. Use a file name exactly as the reader memory lists it, or leave note empty for a new one.");
        }

        if (clash is not null && clash != current)
        {
            return new MemoryEdit($"Not saved: the note {clash.Note.FileName} already has these keywords today. Update that one, or forget_memory one of the two.");
        }

        var notice = $"🧠 Оновив нотатку: «{built!.Title}»";
        if (clash == current)
        {
            return await store.TryReplaceAsync(chatId, current, built.Text, ct)
                ? new MemoryEdit($"Updated {built.FileName}.", notice)
                : new MemoryEdit("Not saved: the note changed meanwhile. Try again.");
        }

        // A new name: the new file is written first, so a failure in the middle leaves the old note rather than nothing.
        if (!await store.TryCreateAsync(chatId, built, ct))
        {
            return new MemoryEdit("Not saved: the memory changed meanwhile. Try again.");
        }

        if (!await store.TryDeleteAsync(chatId, current, ct))
        {
            log.LogWarning("The replaced memory note could not be removed, both versions are kept");
        }

        return new MemoryEdit($"Updated, now {built.FileName}.", notice);
    }

    public async Task<MemoryEdit> ForgetAsync(long chatId, string? note, CancellationToken ct)
    {
        var notes = await ListAsync(chatId, ct);
        if (notes.FirstOrDefault(n => MemoryNoteFormat.SameName(n.Note.FileName, note)) is not { } current)
        {
            return new MemoryEdit("There is no such note. Use a file name exactly as the reader memory lists it.");
        }

        return await store.TryDeleteAsync(chatId, current, ct)
            ? new MemoryEdit($"Removed {current.Note.FileName}.", $"🧠 Забув: «{current.Note.Title}»")
            : new MemoryEdit("Not removed: the note changed meanwhile. Try again.");
    }

    private async Task<DateOnly> TodayAsync(CancellationToken ct)
    {
        var zone = LocalTime.Zone((await options.GetAsync(ct)).TimeZone);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone).DateTime);
    }

    // The reader profile of ADR-0015 becomes the first note of an empty memory, once. A failure here never fails the question:
    // the profile stays where it is and the move is tried again with the next question.
    private async Task<bool> TryMoveLegacyProfileAsync(long chatId, CancellationToken ct)
    {
        try
        {
            if (await store.LoadLegacyProfileAsync(chatId, ct) is not { } profile)
            {
                return false;
            }

            var lines = profile.Text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0)
                .Select(l => l.Length > MemoryNoteFormat.MaxLineChars ? l[..MemoryNoteFormat.MaxLineChars] : l)
                .Take(MemoryNoteFormat.MaxFactLines).ToList();
            var day = DateOnly.FromDateTime(profile.LastModified.UtcDateTime);
            if (lines.Count > 0 && MemoryNoteFormat.TryBuild("Про мене", ["about-me"], lines, day, out var note, out _))
            {
                await store.TryCreateAsync(chatId, note!, ct);
            }

            await store.TryDeleteLegacyProfileAsync(chatId, profile, ct);
            log.LogInformation("The reader profile was moved into the reader memory");
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "The reader profile could not be moved into the reader memory");
            return false;
        }
    }
}
