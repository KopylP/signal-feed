# Settings from the Telegram bot Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The owner changes a useful part of the settings (sources, thresholds, models, chat limits) from the bot chat with inline buttons; the values are stored in a blob and the jobs use them from the next run, with no restart.

**Architecture:** A blob `settings/overrides.json` holds overrides; `IEffectiveOptions` overlays them on the App Settings `AgentOptions`, validates the result, and caches it for 30 s. Jobs take one snapshot per run. A new feature folder `SettingsMenu/` answers `/settings`, button presses (`callback_query`) and the typed reply to a pending question; navigation is stateless (in `callback_data`), only "waiting for text" is stored.

**Tech Stack:** .NET 10 isolated Azure Functions, Telegram.Bot, Azure.Storage.Blobs (ETag), Microsoft.Extensions.AI, xUnit with hand-written fakes.

**Spec:** `docs/superpowers/specs/2026-11-25-telegram-settings-design.md`

## Global Constraints

- Work on a feature branch, never commit to `main`; no `Co-Authored-By` or other attribution line in commits (user rule overrides the harness reminder).
- `TreatWarningsAsErrors`, `Nullable`; no `#pragma`, no `!`. One sealed type per file, file-scoped namespace matching the folder (root `PostAgent`).
- `Shared/` never references a feature. Feature folders do not reference each other; they share types in `Shared/`.
- I/O methods are `async`, take `CancellationToken ct` last; `catch (Exception ex) when (ex is not OperationCanceledException)`.
- Time only from `TimeProvider`. Log with message templates; log setting keys, never typed text other than as a setting value; no secrets.
- Telegram texts are Ukrainian; code, comments, logs, docs are English. Escape every user text with `TelegramHtml.Escape`.
- `callback_data` is at most 64 bytes. Pending question expires after 10 minutes. Cache of `IEffectiveOptions` is 30 s. At most 3 ETag retries.
- Not editable from the bot: schedules, prompts, secrets, `ManualRunsEnabled`, `TelegramChatId`, `RetentionDays`, Telegram account data, storage names.
- Models are chosen from buttons: `gpt-5.4-nano`, `gpt-6.1-terra`, `gpt-6.1-sol`. A model from an App Setting that is not on the list is shown and kept.
- Without the blob everything behaves as before. No new required App Setting. The one manual step: `setWebhook` again with `allowed_updates=["message","callback_query"]`.
- Tests: no mocking libraries, fakes in `tests/PostAgent.Tests/Fakes/`, `FakeTimeProvider`, test names in `Snake_case` sentences. Build: `dotnet build -c Release`; tests: `dotnet test -c Release`; send command output to a temp file and read only what is needed.

## Review Focus

1. Each value is in range but the combination breaks `AlertWindowHours + AlertCooldownHours <= RetentionDays * 24`: the edit is rejected, the message names the fields, the blob is unchanged (Task 2, Task 4).
2. The blob is corrupt, unreadable or makes the options invalid: jobs fall back to the App Settings with a warning, `/settings` still opens and says the overrides are ignored (Task 3, Task 4).
3. A callback or text from another chat, a group, or an old menu message: ignored, nothing changes (Task 8).
4. A pending question has expired, was cancelled by a `/command`, or Telegram re-sends the answer: the text is not saved twice and does not reach the chat as a question (Task 8).
5. A source typed as `@Name`, `t.me/name`, `HTTPS://T.ME/Name`, a duplicate, a too short name, the last source, or one removed in the meantime (Task 9).

---

### Task 1: Catalog and overrides model

**Files:**
- Create: `src/PostAgent.Functions/Shared/Settings/SettingGroup.cs`, `SettingKind.cs`, `SettingChoice.cs`, `SettingDefinition.cs`, `SettingsCatalog.cs`, `SettingsOverrides.cs`, `SourceEntry.cs`, `PendingInput.cs`, `SettingsOverridesJson.cs`
- Test: `tests/PostAgent.Tests/Shared/Settings/SettingsCatalogTests.cs`, `SettingsOverridesJsonTests.cs`

**Interfaces:**
- Produces:
  - `enum SettingGroup { Sources, Digest, Alerts, Collecting, Chat, Models }`; `enum SettingKind { Int, Choice, Text, QuietHours }`
  - `record SettingChoice(string Value, string Label)`
  - `record SettingDefinition(string Key, SettingGroup Group, SettingKind Kind, string Label, int Min, int Max, IReadOnlyList<SettingChoice> Choices)` (Min/Max are 0 for non-`Int`; Choices empty for non-`Choice`)
  - `static class SettingsCatalog { IReadOnlyList<SettingDefinition> All; SettingDefinition? Find(string key); IReadOnlyList<SettingDefinition> InGroup(SettingGroup group); }` — the keys of the spec's groups except `Sources`/`AlertSources` (handled by the sources screen); `Int` bounds are read from `RangeAttribute` of `AgentOptions` by reflection.
  - `record SourceEntry(string Name, bool Verified)`; `record PendingInput(string Key, string Action, int MessageId, DateTimeOffset ExpiresAt)`
  - `sealed record SettingsOverrides(IReadOnlyDictionary<string,string> Values, IReadOnlyList<SourceEntry>? Sources, IReadOnlyList<string>? AlertSources, PendingInput? Pending, long LastUpdateId)` with `static SettingsOverrides Empty`
  - `static class SettingsOverridesJson { string Write(SettingsOverrides o); bool TryRead(string json, out SettingsOverrides overrides); }`

- [ ] **Step 1: Write failing tests**
  - `Reads_the_int_bounds_from_the_range_attributes`: `Find("DigestSize")` has `Min 1`, `Max AgentOptions.MaxDigestItems`; `Find("AlertThreshold")` is `0..10`.
  - `Every_catalog_key_is_a_property_of_AgentOptions` (Theory over `All`) and `Catalog_has_no_secret_or_infrastructure_key` (none of the keys in Global Constraints "Not editable").
  - `The_model_settings_are_choices_of_the_three_listed_models` for `OpenAIModel` and `OpenAIChatModel`; `ChatReasoningEffort` choices are `none, low, medium, high`.
  - `Round_trips_overrides_through_json` (values, sources with flags, alert sources, pending, last update id) and `Rejects_broken_json_without_throwing`.
- [ ] **Step 2: Run them, expect FAIL (types missing):** `dotnet test -c Release --filter "FullyQualifiedName~Shared.Settings"`
- [ ] **Step 3: Implement the types above.** `Int` keys: `DigestSize`, `InterestingThreshold`, `AlertThreshold`, `BurstMinPosts`, `AlertWindowHours`, `AlertCooldownHours`, `AlertMaxPerRun`, `MinPostChars`, `MaxPostChars`, `MaxItemsPerGroup`, `MaxBundleChars`, `ScoringBatchSize`, `ChatMaxToolCalls`, `ChatMaxMessages`; `Choice`: `ChatReasoningEffort`, `OpenAIModel`, `OpenAIChatModel`; `QuietHours`: `AlertQuietHours`; `Text`: `AlertTimeZone`. Ukrainian labels.
- [ ] **Step 4: Run them, expect PASS.**
- [ ] **Step 5: Commit** `Add the catalog of settings editable from Telegram`

### Task 2: Applying and checking overrides

**Files:**
- Create: `src/PostAgent.Functions/Shared/Settings/OverridesApplier.cs`
- Modify: `src/PostAgent.Functions/Shared/Settings/AgentOptions.cs` (add `AgentOptions Clone()` if there is no copy; a shallow copy is enough)
- Test: `tests/PostAgent.Tests/Shared/Settings/OverridesApplierTests.cs`

**Interfaces:**
- Consumes: Task 1 types.
- Produces: `static class OverridesApplier { AgentOptions Apply(AgentOptions app, SettingsOverrides o); IReadOnlyList<string> Check(AgentOptions candidate); }` — `Apply` returns a copy: `Values` parsed per catalog kind (an unparsable or unknown key is skipped), `Sources`/`AlertSources` replace the CSV when not null. `Check` runs `DataAnnotations` (`Validator.TryValidateObject` with `validateAllProperties`) and `AgentOptionsValidator`, returns messages that name the fields (empty = valid).

- [ ] **Step 1: Write failing tests**
  - `Applies_a_number_a_choice_and_the_source_lists`
  - `Keeps_the_app_setting_when_the_key_is_not_overridden` and `Skips_an_unknown_key_and_an_unparsable_number`
  - `Check_names_both_fields_when_window_plus_cooldown_exceeds_retention` (`AlertWindowHours 20`, `AlertCooldownHours 10`, `RetentionDays 1` → message contains `AlertWindowHours` and `AlertCooldownHours`)
  - `Check_rejects_a_number_out_of_range` (`DigestSize 13`) and `Check_accepts_valid_options` (use `TestData`'s valid settings).
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement.** Reuse `AgentOptionsValidator.Validate(null, options)` for the cross-field rules.
- [ ] **Step 4: Run, expect PASS.**
- [ ] **Step 5: Commit** `Apply and check settings overrides`

### Task 3: Overrides blob store

**Files:**
- Create: `Shared/Settings/IOverridesStore.cs`, `OverridesHandle.cs`, `Shared/Settings/Infrastructure/BlobOverridesStore.cs`; `tests/PostAgent.Tests/Fakes/InMemoryOverridesStore.cs`
- Modify: `Shared/Infrastructure/StorageNames.cs` (`SettingsContainer = "settings"`, `SettingsBlob = "overrides.json"`), `StorageRegistration.EnsureLocalResourcesAsync` (create the container)
- Test: `tests/PostAgent.Tests/Shared/Settings/InMemoryOverridesStoreTests.cs`

**Interfaces:**
- Produces: `sealed record OverridesHandle(SettingsOverrides Value, string ETag)`; `interface IOverridesStore { Task<OverridesHandle?> LoadAsync(CancellationToken ct); Task<bool> TrySaveAsync(OverridesHandle? current, SettingsOverrides updated, CancellationToken ct); }` — `LoadAsync` returns null when the blob is missing or unreadable (the latter logs a warning). `TrySaveAsync(null, …)` creates only if absent (`IfNoneMatch *`); otherwise `IfMatch current.ETag`; returns false on 404/409/412. Follow `BlobChatStateStore`.
- The fake mimics ETag semantics (version counter) so Tasks 4, 8, 9 can test conflicts.

- [ ] **Step 1: Write failing tests on the fake:** `Saves_when_the_etag_matches`, `Refuses_a_save_with_a_stale_etag`, `Creates_only_when_absent`. (The blob adapter has no Azure-free test; keep it a thin copy of `BlobChatStateStore`.)
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement interface, handle, blob store, the fake, container name, local creation.** Register in Task 4. Infrastructure (Terraform) needs no change if the other blob containers are created in code on Azure; check `infra/` and, if `chat-state` is declared there, declare `settings` the same way (no standing cost).
- [ ] **Step 4: Run, expect PASS; run `ArchitectureTests`.**
- [ ] **Step 5: Commit** `Store settings overrides in a blob`

### Task 4: Effective options and the editor

**Files:**
- Create: `Shared/Settings/IEffectiveOptions.cs`, `EffectiveOptions.cs`, `SettingsEditor.cs`, `EditResult.cs`, `SettingsServices.cs`; `tests/PostAgent.Tests/Fakes/FakeEffectiveOptions.cs`
- Modify: `ServiceCollectionExtensions.cs` (call `AddSettings()`), `tests/PostAgent.Tests/CompositionTests.cs` if the graph needs it
- Test: `tests/PostAgent.Tests/Shared/Settings/EffectiveOptionsTests.cs`, `SettingsEditorTests.cs`

**Interfaces:**
- Consumes: Tasks 1-3.
- Produces:
  - `interface IEffectiveOptions { Task<AgentOptions> GetAsync(CancellationToken ct); Task<AgentOptions> GetFreshAsync(CancellationToken ct); void Invalidate(); }` — `GetAsync` caches 30 s by `TimeProvider`; if the blob is missing, unreadable or the applied result fails `OverridesApplier.Check`, it returns the App Setting options and logs a warning.
  - `sealed record EditResult(bool Ok, string? Error)`
  - `sealed class SettingsEditor(IOverridesStore store, IOptions<AgentOptions> app, IEffectiveOptions effective, TimeProvider time, ILogger<SettingsEditor> log)` with `Task<EditResult> SetValueAsync(string key, string value, CancellationToken ct)`, `ResetAsync(string key, CancellationToken ct)`, `ResetGroupAsync(SettingGroup group, CancellationToken ct)`, `MarkSourceVerifiedAsync(string name, CancellationToken ct)`, plus the pending and last-update-id operations of Task 8 (`SetPendingAsync`, `ClearPendingAsync`, `TryAdvanceUpdateIdAsync(long id)`) and the source operations of Task 9. Every method is read-modify-write with up to 3 retries on a failed `TrySaveAsync`; `SetValueAsync` validates the candidate with `OverridesApplier.Check` and refuses without saving; `ResetAsync` is always allowed; a successful save calls `effective.Invalidate()`.

- [ ] **Step 1: Write failing tests:**
  - `Returns_the_app_settings_when_there_is_no_blob`; `Applies_overrides_from_the_blob`; `Caches_for_thirty_seconds_and_reloads_after` (`FakeTimeProvider`); `GetFresh_ignores_the_cache`; `Falls_back_to_the_app_settings_when_the_overrides_make_the_options_invalid` (and a warning is logged).
  - `Saves_a_valid_value_and_invalidates_the_cache`; `Refuses_a_value_that_breaks_a_cross_field_rule_and_leaves_the_blob_unchanged` (Review Focus 1); `Reset_is_allowed_even_when_the_app_settings_are_invalid`; `Retries_after_an_etag_conflict` (fake store fails the first save); `Gives_up_after_three_conflicts_with_an_error_result`.
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement.** `AddSettings()` registers `IOverridesStore → BlobOverridesStore`, `IEffectiveOptions → EffectiveOptions`, `SettingsEditor` as singletons.
- [ ] **Step 4: Run, expect PASS; run the whole suite (`CompositionTests`).**
- [ ] **Step 5: Commit** `Overlay Telegram overrides on the App Settings`

### Task 5: Jobs read the effective options; the model is chosen per call

**Files:**
- Create: `Shared/Ai/ModelSelectingChatClient.cs`
- Modify: `Collecting/CollectorJob.cs`, `Collecting/SourceCollector.cs`, `Digest/DigestJob.cs`, `Alerts/AlertJob.cs`, `Chat/Infrastructure/ChatAgent.cs`, `Chat/Infrastructure/ChatTools.cs`, `Scoring/*` where it reads editable options, `Shared/Ai/AiServices.cs`
- Test: `tests/PostAgent.Tests/Shared/Ai/ModelSelectingChatClientTests.cs`; update the existing job tests to supply `FakeEffectiveOptions`

**Interfaces:**
- Consumes: `IEffectiveOptions.GetAsync`.
- Produces: `sealed class ModelSelectingChatClient(IChatClient inner, IEffectiveOptions effective, Func<AgentOptions,string> pickModel) : DelegatingChatClient(inner)` — on `GetResponseAsync` and `GetStreamingResponseAsync` it copies the `ChatOptions` and sets `ModelId` to `pickModel(await effective.GetAsync(ct))`.

- [ ] **Step 1: Write failing tests:** `Sets_the_model_id_from_the_effective_options_on_every_call`, `Does_not_change_the_callers_options_object`, `Uses_the_new_model_after_the_options_change`. For each job keep its existing tests green and add one: `<Job>_uses_a_value_from_the_effective_options` (for example `DigestJob` with `DigestSize` changed in the fake).
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement.** Each job takes one snapshot at the start of `RunAsync` and passes it down (change `PostBuilder`/selector calls to the snapshot, not to `IOptions`); `SourceCollector.CollectAsync` gets the snapshot or reads it once. In `AiServices`, wrap both registered clients with `ModelSelectingChatClient` (`o => o.OpenAIModel` and `o => o.OpenAIChatModel`); the keyed chat client is still built with the Responses API (ADR-0011) and `ChatModelRequestTests` must stay green. Secrets and infrastructure values keep `IOptions<AgentOptions>`.
- [ ] **Step 4: Run the whole suite, expect PASS.**
- [ ] **Step 5: Commit** `Read editable settings at the start of each run`

### Task 6: Telegram panel (buttons, edit, answer a callback)

**Files:**
- Create: `Shared/Notifications/InlineKeyboard.cs`, `KeyboardButton.cs`, `ITelegramPanel.cs`, `Shared/Notifications/Telegram/TelegramPanel.cs`; `tests/PostAgent.Tests/Fakes/RecordingPanel.cs`
- Modify: `ITelegramNotifier` (add `Task SendAsync(string html, InlineKeyboard keyboard, CancellationToken ct)`), `TelegramNotifier`, `tests/.../Fakes/RecordingNotifier.cs`, `NotificationsServices.cs`
- Test: `tests/PostAgent.Tests/Shared/Notifications/TelegramPanelTests.cs` (only the pure mapping if one exists)

**Interfaces:**
- Produces: `record KeyboardButton(string Text, string Data)`; `record InlineKeyboard(IReadOnlyList<IReadOnlyList<KeyboardButton>> Rows)` with `static InlineKeyboard Empty`; `interface ITelegramPanel { Task<int> SendAsync(long chatId, string html, InlineKeyboard keyboard, CancellationToken ct); Task EditAsync(long chatId, int messageId, string html, InlineKeyboard keyboard, CancellationToken ct); Task AnswerCallbackAsync(string callbackQueryId, string? text, CancellationToken ct); }` — `EditAsync` treats "message is not modified" as success and throws other failures; same HTTP client, resilience (no POST retry) and logging rules as `TelegramNotifier` (length and time only).

- [ ] **Step 1: Write failing tests** for any pure mapping (`InlineKeyboard` → Telegram markup, a button over 64 bytes of `Data` throws `ArgumentException`: `Rejects_callback_data_longer_than_64_bytes`).
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement; register `TelegramPanel` through `AddHttpClient<ITelegramPanel, TelegramPanel>()` with the same resilience as the notifier.**
- [ ] **Step 4: Run the whole suite, expect PASS.**
- [ ] **Step 5: Commit** `Send buttons and edit messages through the bot`

### Task 7: Routing of callbacks and a handler list

**Files:**
- Modify: `Shared/Notifications/IIncomingMessageHandler.cs` (return `Task<bool>`: true when the update was handled), `Pairing/PairingWebhook.cs`, `Chat/ChatInbox.cs`, `tests/.../Pairing/*Webhook*Tests.cs`, `tests/.../Chat/ChatInboxTests.cs`
- Test: `tests/PostAgent.Tests/Pairing/PairingWebhookRoutingTests.cs`

**Interfaces:**
- Produces: the webhook takes `IEnumerable<IIncomingMessageHandler>` (registration order) and gives the update to each until one returns true. `/start` still goes to pairing first, only for messages, also while unpaired. An update with neither `Message` nor `CallbackQuery` returns 200 as today. `ChatInbox` returns true when it handled the message, false for a callback or a non-private chat.

- [ ] **Step 1: Write failing tests:** `Gives_a_callback_query_to_the_handlers`, `Stops_at_the_first_handler_that_handled_the_update`, `Still_sends_start_to_pairing_before_any_handler`, `Ignores_an_update_without_message_or_callback`, `Chat_inbox_does_not_handle_a_callback_query`.
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the whole suite, expect PASS.**
- [ ] **Step 5: Commit** `Route button presses to the handlers`

### Task 8: Settings menu (screens, callback codec, state machine)

**Files:**
- Create under `src/PostAgent.Functions/SettingsMenu/`: `CallbackData.cs`, `MenuScreens.cs`, `Screen.cs`, `MenuController.cs`, `SettingsInbox.cs`, `SettingsMenuTexts.cs`, `SettingsMenuServices.cs`
- Modify: `ServiceCollectionExtensions.cs` (`AddSettingsMenu()` registered **before** `AddChat()`, so the menu gets updates first)
- Test: `tests/PostAgent.Tests/SettingsMenu/CallbackDataTests.cs`, `MenuScreensTests.cs`, `MenuControllerTests.cs`, `SettingsInboxTests.cs`

**Interfaces:**
- Consumes: `SettingsEditor`, `IEffectiveOptions.GetFreshAsync`, `IOverridesStore`, `ITelegramPanel`, `ChatResolver`, `TimeProvider`, catalog.
- Produces:
  - `abstract record MenuAction` with cases `Main`, `OpenGroup(SettingGroup)`, `OpenSources`, `SetInt(string Key, int Value)`, `Choose(string Key, int ChoiceIndex)`, `AskText(string Key)`, `Reset(string Key)`, `ResetGroup(SettingGroup)`, `Cancel` and the source cases of Task 9; `static class CallbackData { string Encode(MenuAction a); MenuAction? Decode(string data); }` — prefixes `m`, `g:`, `s`, `n:<key>:<value>`, `c:<key>:<i>`, `t:<key>`, `r:<key>`, `rg:<group>`, `x`; unknown or malformed data decodes to null.
  - `record Screen(string Html, InlineKeyboard Keyboard)`; `static class MenuScreens { Screen Main(...); Screen Group(SettingGroup g, AgentOptions effective, SettingsOverrides o); Screen Ask(SettingDefinition d); }` — a parameter shows its value and `✎` when set from the bot; `Int` rows have `−`/`+` buttons whose data carries the target value clamped to the range; `Choice` rows list the options with the current one marked; a "back" button on every screen; when the blob is invalid the main screen says that the bot's values are ignored.
  - `sealed class MenuController { Task HandleCommandAsync(long chatId, string text, CancellationToken ct); Task HandleCallbackAsync(CallbackQuery q, CancellationToken ct); Task<bool> HandleTextAsync(long chatId, string text, CancellationToken ct); }`; `sealed class SettingsInbox : IIncomingMessageHandler` authorizes and dedupes, then calls the controller.
- State machine (stored as `SettingsOverrides.Pending`): `Idle` ↔ `AwaitingText(Key, Action, MessageId, ExpiresAt = now + 10 min)`.
  - `AskText` (or a text-kind button) sets pending, replacing any earlier one, and edits the menu message into the question.
  - `HandleTextAsync` with an unexpired pending: validate through `SettingsEditor.SetValueAsync` (or the sources operation of Task 9), on success clear pending and edit the menu message to the group screen; on a wrong value keep pending and say why. With no pending or an expired one it returns false (the chat handles it).
  - `Cancel`, "back", or any other `/command` clears pending. If an edit fails (message gone or too old) the controller sends a new message.
  - `SettingsInbox`: only a private chat equal to the bound chat (for a callback, `callback_query.message.chat`); `TryAdvanceUpdateIdAsync(update.Id)` false → handled, nothing done; always `AnswerCallbackAsync` for a callback so the button stops spinning; the `/settings` and `/cancel` commands.

- [ ] **Step 1: Write failing tests**
  - `CallbackDataTests`: `Round_trips_every_action`; `Every_button_of_every_screen_has_data_up_to_64_bytes` (build all screens with the longest source name); `Rejects_unknown_and_malformed_data`.
  - `MenuScreensTests`: `Marks_a_value_set_from_the_bot`; `Clamps_the_plus_button_at_the_maximum`; `Marks_the_current_model`; `Shows_a_model_that_is_not_on_the_list_and_keeps_it`.
  - `MenuControllerTests`: `Plus_sets_the_target_value_and_a_double_tap_changes_nothing_more`; `Choosing_a_model_saves_it`; `Text_while_pending_is_saved_and_clears_pending`; `Wrong_text_keeps_pending_and_explains`; `Expired_pending_sends_the_text_on_to_the_chat` (returns false); `A_second_ask_replaces_the_first_pending`; `Another_command_cancels_pending`; `Sends_a_new_message_when_the_edit_fails`; `Rejected_value_shows_the_field_names`.
  - `SettingsInboxTests`: `Ignores_a_callback_from_another_chat`; `Ignores_a_group_chat`; `Ignores_a_replayed_update_id` (the re-sent answer is not saved twice and not sent to the chat); `Answers_every_callback_query`; `Opens_the_menu_on_settings_command`; `Does_nothing_while_the_agent_is_not_paired`.
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement.** Ukrainian texts in `SettingsMenuTexts`; everything escaped with `TelegramHtml.Escape`.
- [ ] **Step 4: Run the whole suite, expect PASS; run `ArchitectureTests` (the new feature references only `Shared/`).**
- [ ] **Step 5: Commit** `Change settings from the Telegram bot`

### Task 9: Sources screen and rules

**Files:**
- Create: `SettingsMenu/SourceRules.cs`
- Modify: `Shared/Settings/SettingsEditor.cs`, `SettingsMenu/MenuScreens.cs`, `MenuController.cs`, `CallbackData.cs`, `Collecting/SourceCollector.cs`, `Collecting/CollectorJob.cs`, `Collecting/CollectionFailureMessage.cs`, `Collecting/ISourceCursors.cs` and its table implementation, `tests/.../Fakes/` (the cursor fake)
- Test: `tests/PostAgent.Tests/SettingsMenu/SourceRulesTests.cs`, `SettingsEditorSourcesTests.cs`; extend `CollectorJobTests`, `SourceCollectorTests`, `CollectionFailureMessageTests`

**Interfaces:**
- Produces:
  - `static class SourceRules { string? TryParse(string typed); }` — uses `SourceNames.Normalize`, then requires 5–32 characters of letters, digits and `_`; null when invalid.
  - `SettingsEditor`: `AddSourceAsync(string name)` (appends `SourceEntry(name, false)`; the first change copies the App Setting sources as verified), `RemoveSourceAsync(string name)` (refuses the last source; also removes it from `AlertSources`), `ToggleAlertSourceAsync(string name)`; all idempotent and safe when the name vanished meanwhile.
  - `ISourceCursors.DeleteExceptAsync(IReadOnlyCollection<string> keep, CancellationToken ct)`; `CollectorJob` calls it at the start with the effective list.
  - `SourceCollector` calls `editor.MarkSourceVerifiedAsync(name)` after a read that ended without an exception (also with no new posts); a failure of the mark is logged and never fails the collection.
  - `CollectionFailureMessage.Format` gains an overload returning the text plus an `InlineKeyboard` with "🗑 Видалити @name" (`CallbackData` for source removal) for each failed source that is not verified; `CollectorJob` sends it through the keyboard overload of the notifier.
  - Sources screen: each source with `✅`/`⏳`, buttons `🚨` (on/off) and `🗑`, and `➕ Додати`.

- [ ] **Step 1: Write failing tests**
  - `SourceRulesTests` (Theory): `@Name`, `t.me/name`, `HTTPS://T.ME/Name`, `name/` normalize to `name`; `abc`, `bad name`, `імя`, `a-b-c-d-e` are rejected.
  - `SettingsEditorSourcesTests`: `Adds_a_new_source_as_not_verified`; `Ignores_a_duplicate`; `Refuses_to_remove_the_last_source`; `Removing_a_source_removes_it_from_the_alert_list`; `Removing_a_missing_source_is_not_an_error`; `The_first_change_keeps_the_app_setting_sources_as_verified`; `Toggles_an_alert_source`.
  - Collecting: `Marks_a_source_verified_after_a_read_with_no_new_posts`; `Does_not_mark_a_source_whose_read_failed`; `A_failed_mark_does_not_fail_the_collection`; `Deletes_cursors_of_sources_that_are_no_longer_listed`; `The_failure_message_offers_removal_only_for_unverified_sources`.
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the whole suite, expect PASS.**
- [ ] **Step 5: Commit** `Manage sources from the Telegram bot`

### Task 10: ADR, docs, changelog

**Files:**
- Create: `docs/adr/0012-override-settings-from-the-telegram-bot-through-a-blob.md` (use the `write-adr` skill; supersedes nothing; mention rejected options B and C from the spec)
- Modify: `docs/user-guide/configuration.md` (a section "Change settings from Telegram": `/settings`, what is editable, what is not, "reset", precedence bot → App Setting → code, the ignored-overrides case), `docs/user-guide/installation.md` (line 96: `allowed_updates=["message","callback_query"]` and a note for an existing install to call `setWebhook` again), `docs/user-guide/troubleshooting.md` (buttons do nothing → webhook has no `callback_query`), `docs/developer-guide/architecture.md` and `code-structure.md` (the `SettingsMenu/` feature, the blob, `IEffectiveOptions`), `src/PostAgent.Functions/CLAUDE.md` (a short "Settings from Telegram" rule list: jobs read one snapshot per run; new editable parameter goes into `SettingsCatalog`; models are a fixed list), `docs/README.md` if it lists ADRs, `CHANGELOG.md` (use the `write-changelog` skill; `Setup required`: call `setWebhook` again with both update types)
- Keep every `CLAUDE.md` at about 60 lines at most.

- [ ] **Step 1: Write the ADR, the docs and the changelog section.**
- [ ] **Step 2: Run the pre-PR gate with the `check-before-pr` skill:** `dotnet restore && dotnet build -c Release --no-restore`, `dotnet test -c Release`, `dotnet format --verify-no-changes --no-restore` (each to a temp file; read the tail and the failures only). If `infra/` changed, `terraform -chdir=infra fmt -check`.
- [ ] **Step 3: Commit** `Document settings changes from the Telegram bot`

## Self-review notes

- Spec coverage: catalog and ranges (T1), applying and validation (T2), blob and ETag (T3), effective options, cache, editor, reset (T4), jobs and per-call model (T5), buttons and edit (T6), `callback_query` routing and `/start` order (T7), menu, state machine, authorization, duplicates (T8), sources and verification and cursors (T9), ADR, docs, `setWebhook` step, changelog (T10).
- Types used across tasks match: `SettingsOverrides`, `OverridesHandle`, `IOverridesStore`, `IEffectiveOptions`, `SettingsEditor`, `EditResult`, `ITelegramPanel`, `InlineKeyboard`, `CallbackData`/`MenuAction`.
