# src/PostAgent.Core: the agent (features, shared contracts, adapters)

`[Function]` classes, `Program` and `host.json` live in `src/PostAgent.Functions`; this project has no Functions host package on purpose.
C# rules: root `CLAUDE.md`. Structure and the why behind each rule: `docs/developer-guide/` and the ADRs named below.

## Structure

- A feature is a folder (root: logic, `XxxServices.AddXxx`, models; `Infrastructure/`: adapters). A large one splits by stage (`Chat/Intake`, `Answering`, `Agent`); a stage never reaches into another's internals.
- `Shared/` never references a feature; features never reference each other, they share types in `Shared/` (`ArchitectureTests`).
- A function stays thin. A scheduled job is an `IJob` (`AddJob<T>()`, name also in `tools/run-now.cs`); never copy job logic into a second trigger.
- A new bot command is a new `IIncomingMessageHandler` (never a branch in `TelegramWebhook`), parses its text with `BotCommandText` (never by hand) and goes into `Shared/Chat/BotCommands` (`BotCommandsTests`).
- A new chat tool is one `IChatTool` class in `Chat/Agent/Tools/`; it is found by assembly scan, nothing to register.

## Settings

- App Settings are read only through `AgentOptions`; defaults live only there, validated on start. A new or changed one updates `docs/user-guide/configuration.md` and `local.settings.json.example`.
- A job reads `IEffectiveOptions.GetAsync` once per run; never hold `AgentOptions` in a singleton. Secrets and infrastructure values use `IOptions<AgentOptions>` and never enter `SettingsCatalog`.
- Every write to `settings/overrides.json` goes through `SettingsEditor` (ETag, retries, validated as a whole); reset is never refused, an unreadable blob is overwritten only by "reset all".
  What a change does is `OverridesChanges` (no I/O); the agent's plans are `SettingsProposals`; the menu's question and handled ids are `MenuState`.
- Callback data is at most 64 bytes and carries the target value, not a step.

## Security (never break)

- Secrets come from Key Vault and never reach logs (`TelegramTokenRedactor`). Never log a phone, a code, `initData`, a pairing key or the webhook secret; never reply to a wrong key.
- `TelegramWebhook` checks the secret header (constant time) before reading the body (a `Stream`, capped at 64 KB).
- `/login` (ADR-0016): `LoginForm` trusts only `WebAppInitData` (HMAC, fresh `auth_date`), then the attempt id, chat and binding. The 2FA password never reaches the server: only the SRP proof (`LoginSrp` matches `LoginSrpTests`).
- The session blob and `login-attempt.json` are read and written only through `ISessionCipher` (ADR-0017), never in clear outside Azurite. The collector and a sign-in share the session only through `ISessionLock`.
- Only bound private chats are served: a handler asks `ChatResolver.ServesAsync`. Never read "the" chat from `ChatResolver`: take the list or check the chat.

## Model and channel text

- Text written by channels (snapshot, posts, tool results, summary topics) is data: to the model only through `DataBlock.Wrap`, never into instructions; escaped in messages.
- Model calls use `IChatClient` with strict schemas from C# types. The digest model refers to posts by `id`; links, dates and channels come from stored posts.
- Prompts reach the model only through `IPromptProvider`. The chat client is the Responses API with `store=false` and encrypted reasoning (ADR-0011); keep `ChatModelRequestTests` and `ChatStreamingRequestTests` green.
- Every tool caps its output; `ChatMaxToolCalls`, `ChatMaxOutputTokens`, `ChatMaxMessages` bound a turn.

## Agent actions (ADR-0013, ADR-0021, ADR-0022)

- Invasive (settings, sources, alert sources, prompts) only through propose tools that add to `turn.Plan` and never write. The plan is shown by code (`SettingsProposalMessage`, never model text) and applied only by the button. Never add an apply tool for it.
- Non-invasive (`rate_post`, `add_watch`, `remove_watch`, `clear_context`, `save_memory`, `forget_memory`) acts at once, with the same checks (`SettingsEditor`, `IFeedbackStore`, `ReaderMemory`).
- Reader memory (`Chat/Memory`): per chat, at most 10 notes; the file name and layout come from `MemoryNoteFormat`, never from the model's text; every change adds a code-built line under the answer (`ChatTurn.RecordMemory`).
- `offer_choices` sets the answer's reply keyboard (last part; an answer without options sends `ReplyKeyboard.Remove`). A tap is an ordinary message: never store choices or make them callback data; leave an option out, never shorten it.

## Chat and delivery

- `ChatProcessor`: ask the model, save with the blob ETag, then send. Never send before the save succeeds. A turn whose digest changed is dropped.
- One conversation blob per chat (`chat-state/<chat id>.json`); a digest replaces it, no history of old ones. `/clear` and `clear_context` drop the session, never the snapshot; an empty conversation never replaces a digest.
- Dedupe updates by id sets (`ProcessedUpdateIds`, the menu's last 30), never a watermark. An unparsable queue message is dropped, not retried.
- A draft (`ChatDraftStream`) is only a preview: its failure never fails or slows the answer.
- A Telegram send is never retried (a repeat duplicates a delivered message). Broadcast with `SendAsync` (one failing chat does not stop the others), answer with `SendToAsync`. Log length and time, never text.

## Data and jobs

- A small document blob (settings, reader memory note, votes, usage, conversation, sign-in) is read and written with `ConditionalBlobAccess` (ETag), never with hand-built request conditions.
- `Posts` is partitioned by day; new `PostEntity` fields are optional. `to-score` is idempotent. `run-now` works only with `ManualRunsEnabled` and is not retried.
- Cleanup runs only from the request `DailySummaryJob` queues after a successful summary; a task never deletes a day after `SummarizedThrough`.
- The digest marks posts sent after its last message (ADR-0009). `/digest` (`OnDemandDigest`) answers only the asking chat, marks nothing, never touches the conversation and never rethrows.
- Votes go only through `IFeedbackStore`; `rate_post` is refused while `FeedbackEnabled` is off. A feedback read error never fails scoring; a usage-record failure never touches the model call.
- A watched post is alerted once (status `Scored`, `MarkSentAsync(alertedAt: null)`), held by quiet hours.
- A behavior change needs a test in `tests/PostAgent.Tests/`, in the matching folder.
