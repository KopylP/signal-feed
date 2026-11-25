# Settings from the Telegram bot: design

Date: 2026-11-25. Status: draft, waiting for approval.

## Intent

Today every parameter is an Azure App Setting, so changing the list of channels or a threshold means `az` or Terraform and a restart.
The owner wants to change a useful part of them from the bot chat: add a channel when they see one, raise a threshold when the digest is noisy.
Values set in the bot are stored in a blob and override the App Settings; the jobs read them at the start of each run, with no restart.

## Goals

- `/settings` opens one message with inline buttons that is edited in place: groups, then parameters, then a value picker. No flood of messages.
- Numbers change with `−`/`+` buttons inside the ranges of `AgentOptions`; fixed choices (reasoning effort, the models) are buttons; free text (a channel, quiet hours) is typed after a button asks for it.
- Sources are managed in the bot: add, remove, mark as alert source. A new source is added at once as "⏳ not verified"; the first successful read confirms it, a failed read tells the owner and offers "🗑 Remove".
- A value from the bot is checked as a whole (the same `AgentOptionsValidator`) before it is saved, so the blob never holds a value that stops a job.
- Every parameter shows where its value comes from (bot or App Setting/default) and has "reset" back to the App Setting.

## Non-goals and anti-goals

- Not editable from the bot: schedules (read once by `[TimerTrigger]`), prompts, secrets, `ManualRunsEnabled`, `TelegramChatId`, `RetentionDays`, the Telegram account data, storage names.
- No multi-user support (ADR-0008: one bound chat), no audit history, no undo except "reset", no web page.
- Failure would be: a bad value from the bot that stops the app or a job; a callback or text from a chat that is not the bound private chat changing anything; a double tap stepping a number twice; a secret reaching the logs.

## Constraints

- The webhook is public and must answer fast; Telegram resends an update on a timeout. Only the bound private chat is served (the rule `ChatInbox` already follows).
- `Shared/` must not depend on a feature: the effective-options service and the blob store live in `Shared/Settings`, the menu in a new feature folder.
- 17 classes read `IOptions<AgentOptions>` (singleton, fixed at start). Secrets and infrastructure values keep using it; only the jobs that use editable values change.
- `callback_data` is at most 64 bytes. The Telegram texts are Ukrainian; everything else is English. All rules in the root and folder `CLAUDE.md` files apply.
- Without the blob everything behaves exactly as before: no required setting. One manual step: `setWebhook` must be called again with `allowed_updates=["message","callback_query"]` (see below).

## Approaches considered

- **A. Overrides blob plus an effective-options service (chosen by the maintainer).** Explicit, testable, the bot and the jobs read the same thing; the cost is moving the affected jobs to a new call.
- **B. Custom `IConfigurationProvider` with `IOptionsMonitor`.** Consumers barely change, but reloading is hidden, validation bypasses `AgentOptionsValidator`, and a bad blob can stop startup. Rejected.
- **C. Table Storage rows per setting.** Fits numbers, fits badly the "waiting for text" state and the source list with flags. Rejected.

For the menu: a stateless navigation (the screen is in `callback_data`) with a small stored state only for the "waiting for text" step, instead of one big stored state machine.

## Design

**Catalog** (`Shared/Settings/SettingsCatalog`): the editable parameters, each with key, group, kind (`Int`, `Choice`, `Text`, `Sources`, `QuietHours`), Ukrainian label, and for numbers min, max and step. Min and max are read from the `Range` attributes of `AgentOptions`, so the ranges have one home.
Groups: Sources (`Sources`, `AlertSources`), Digest (`DigestSize`, `InterestingThreshold`), Alerts (`AlertThreshold`, `BurstMinPosts`, `AlertWindowHours`, `AlertCooldownHours`, `AlertMaxPerRun`, `AlertQuietHours`, `AlertTimeZone`),
Collecting (`MinPostChars`, `MaxPostChars`, `MaxItemsPerGroup`, `MaxBundleChars`, `ScoringBatchSize`), Chat (`OpenAIChatModel`, `ChatReasoningEffort`, `ChatMaxToolCalls`, `ChatMaxMessages`), Models (`OpenAIModel`).

**Store**: blob `settings/overrides.json`, written with an ETag condition (like `BlobChatStateStore`), read-modify-write with a few retries. Content:
`{ values: { key: string }, sources: [{ name, verified }] | null, alertSources: [name] | null, pending: { key, action, messageId, expiresAt } | null }`.
`null` means "not overridden, use the App Setting". Once `sources` is set, a later change of the `Sources` App Setting is ignored; the menu says so and "reset" returns control to the App Setting.

**`IEffectiveOptions.GetAsync(ct)`** (`Shared/Settings`): the `AgentOptions` from App Settings with the overrides applied, checked by `AgentOptionsValidator`. If the result is invalid (for example the App Setting changed under an override), it logs a warning and returns the App Setting values. Cached in memory for about 30 seconds.
Each job takes one snapshot at the start of a run and passes it down; it does not read again mid-run. Jobs to move: collecting, scoring, digest, alerts, chat.

**Routing**: the webhook also passes `CallbackQuery` updates on (today it drops everything except `Message`). `IIncomingMessageHandler` handlers become an ordered list and each returns whether it handled the update: settings menu first (`/settings`, callbacks, the reply to a pending question), then the chat. `/start` stays with pairing.

**Menu state machine** (feature folder `SettingsMenu/`). State is only `Idle` or `AwaitingText(key, action, messageId, expiresAt)`, stored in `pending`. Transitions:
- Button "add / edit" on a text parameter → `AwaitingText`, the menu message asks for the value.
- A plain text message while `AwaitingText` and not expired → parse, validate with the whole-options check, save, edit the menu message with the result, back to `Idle`. A wrong value keeps the state and says why.
- "Back", "Cancel", any other `/command`, or expiry (10 minutes) → `Idle`. Text with no pending state goes to the chat as before.
- Navigation and number/choice buttons need no state: `callback_data` carries the screen or the target value (`n:DigestSize:8`, not "+1"), so a double tap is harmless. Unknown keys and values outside the catalog are ignored; the value is always re-validated on the server.

**Screens**: main menu with the groups; a group screen listing its parameters with value and marker (`7` or `7 ✎` when set from the bot), buttons per parameter; the sources screen lists each source with `✅` or `⏳` and buttons "🚨" (alert source on/off) and "🗑", plus "➕ Add".

**Sources**: names go through `SourceNames.Normalize` and must look like a Telegram username (5-32 letters, digits, underscore); duplicates are ignored; the last source cannot be removed (the validator needs one); removing a source removes it from the alert list; the collector deletes the cursors of sources that are no longer in the effective list at the start of a run, so a later re-add does not replay old posts and `SettingsMenu` never touches `Collecting`.
The collector marks a source verified after its first successful read (best-effort write). `CollectionFailureMessage` for a source that is not yet verified carries a "🗑 Remove" button; this needs the notifier to send inline buttons.

**Models**: the model parameters (`OpenAIModel`, `OpenAIChatModel`) are `Choice` settings: the owner taps a model on a list of buttons and never types a name. The list is a short fixed list in the catalog (the models already named in the docs: `gpt-5.4-nano`, `gpt-6.1-terra`, `gpt-6.1-sol`), with a one-line hint on cost; the current value is marked, and a model set through an App Setting that is not on the list is still shown and kept. Adding a model to the list is a code change. This removes the need to check a typed name.

**Telegram access**: a new small interface next to `ITelegramNotifier` for what the menu needs: send a message with buttons (returns the message id), edit a message and its buttons, answer a callback query. "Message is not modified" is not an error.

**Logging**: setting keys and the screen, never the text a user typed other than as a setting value; no secrets exist in this feature.

**Tests**: catalog and range reading; applying overrides and falling back on an invalid result; the transition table with a fake store, including expiry and a double tap; ETag conflict retry; every generated `callback_data` is at most 64 bytes; source rules (last source, alert list cleanup, normalization).

**Docs**: ADR "Override settings from the Telegram bot through a blob" (new configuration model); `docs/user-guide/configuration.md` (how to change settings from the bot, what cannot be changed there); developer guide (architecture, code structure); the `CLAUDE.md` of `src/PostAgent.Functions`; a changelog section.

## Decisions made by the assistant (to check)

- Ranges come from the `Range` attributes through reflection instead of a second table.
- Cache 30 seconds; a pending question expires after 10 minutes; at most three ETag retries.
- The "Remove" button appears only for sources that are not yet verified.
- The collector, not the menu, deletes the cursor of a removed source.
- `RetentionDays` stays out of the bot even though `AlertWindowHours + AlertCooldownHours` is limited by it; the error text names the limit.
- Models are chosen from a fixed list of buttons (the maintainer's call), so there is no typed name and no OpenAI check.

## Settled after the builder check

- **Webhook registration.** `installation.md` registers the webhook with `allowed_updates=["message"]`, so buttons reach the app only after `setWebhook` is called again with `["message","callback_query"]`. This is a documented manual step: the installation guide and troubleshooting change, and the changelog section lists it under `Setup required`. `/settings` itself works before that; only the buttons stay dead.
- **Authorization and routing.** `/start` (pairing) is checked first, also while the agent is unpaired. The handler list takes the whole `Update`. A callback is accepted only when `callback_query.message.chat` is the bound chat and is private.
- **Cache.** The menu reads without the cache and a save drops the cache of that instance; the jobs use the 30-second cache, and a stale value on another instance for that long is accepted.
- **Validation.** `Validate` is called on the `AgentOptions` built by hand. "Reset" is always allowed (it is checked against the App Settings only). A failed check names the fields in the message.
- **Pending slot.** A new "add/edit" replaces the old pending question and edits the newest menu message. If an edit fails (message gone or too old), the menu sends a new message.
- **Verified means** the read ended without an exception, including a read with no new posts. A private or non-existent channel fails as it does today.
- **Models** are chosen from buttons, never typed (the maintainer's call): no name check and no OpenAI call in the webhook.
- **Duplicates.** The blob keeps `lastUpdateId` of the last handled settings update and ignores updates up to it; a re-sent answer is then not sent to the chat as a question.

## Left to the builder

Names of types and callback prefixes, exact Ukrainian wording and button layout, how the blob container is created (code or Terraform; no new standing cost), whether `setMyCommands` is registered by the existing webhook tool, and the order of moving jobs to `IEffectiveOptions`.
