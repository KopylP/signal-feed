# src/PostAgent.Functions: Azure Functions

General C# rules are in the root `CLAUDE.md`. Feature structure: `docs/developer-guide/code-structure.md`.

## Features and registration

- A feature is a folder. Its root holds the function, `XxxServices`, pure logic, message formatting, models and interfaces. `Infrastructure/` holds the adapters (Telegram, Azure, model) and the response schemas the adapter produces. A feature with no adapters (`Alerts/`, `Cleanup/`) has no `Infrastructure/`.
- One type per file; the file name is the type name.
- `Shared/` never references a feature. A name or type that both need (a storage resource name, a settings parser, `PostScore`) lives in `Shared/`.
- Each feature has `XxxServices.AddXxx(this IServiceCollection)`, called from `ServiceCollectionExtensions.AddPostAgent`.
- A function stays thin: it takes dependencies through the constructor, calls the logic and catches only what it can handle. Pure logic (selection, formatting) lives in static classes and is tested on its own.
- A scheduled job is an `IJob` class (`Shared/Jobs`) registered with `AddJob<T>()`; its timer function only calls `RunAsync`. `RunNowFunction` starts any job by `Name`, so never copy job logic into a second trigger. A new job name also goes into `tools/run-now.cs`.
- A new cleanup is an `ICleanupTask` class in `Cleanup/`, registered in `CleanupServices`.

## Settings

- All App Settings are read through `AgentOptions` (`Shared/Settings`). Defaults live only there, it is the single source.
- Ranges and required values use DataAnnotations; more complex checks go in `AgentOptionsValidator`. Settings are validated on startup (`ValidateOnStart`).
- Schedules are not part of `AgentOptions`: the `[TimerTrigger("%Name%")]` attributes read them directly.
- When you add or change a parameter, update the table in `docs/user-guide/configuration.md` and `local.settings.json.example`; for Azure see `infra/CLAUDE.md`.
- Secrets (`OpenAIApiKey`, `TelegramBotToken`, `TelegramApiHash`, `TelegramPairingKey`, `TelegramWebhookSecret`) come from Key Vault. Never log them; `TelegramTokenRedactor` strips the bot token from telemetry.

## Public endpoint

- `PairingFunction` is anonymous because Telegram cannot send a function key. Every check lives in `PairingWebhook`: the secret token header first, compared with `SecretComparison` (constant time), before the body is read (the body is passed as a `Stream` for that reason, capped at 64 KB).
- Never reply to a wrong key and never log a key or the secret. The first pairing wins; `TelegramChatId`, when set, overrides the stored chat and turns pairing off (ADR-0008).

## Chat about the digest (ADR-0010)

- The webhook routes `/start` to pairing and every other update to the `IIncomingMessageHandler` list in registration order, the first that returns true wins (`Shared/Notifications`; the settings menu is registered before the chat); `Pairing`, `Digest`, `Chat` and `Cleanup` never reference each other, they share only types in `Shared/` (`Shared/Chat`).
- The conversation is one blob (`chat-state/current.json`): `Digest` overwrites it with a snapshot after the digest is sent, `Cleanup` deletes it when stale. Keep it that way: no history of old conversations.
- `ChatProcessor` order is ask the model, save with the blob ETag, then send. Never send before the save succeeds. A turn whose digest changed is dropped, not redelivered.
- Dedupe handled updates by the set in `ChatState.ProcessedUpdateIds`, never by a watermark. A chat queue message that cannot be parsed is dropped, not retried.
- Anything a channel wrote (the snapshot, tool results) goes to the model through `DataBlock.Wrap`, never into the instructions. A tool gets a cap on its output (`ChatSearchMaxHits`).
- `ChatMaxToolCalls`, `ChatMaxOutputTokens` and `ChatMaxMessages` bound the cost of a turn; the chat model is `OpenAIChatModel` (keyed `IChatClient`, `AiServices.ChatClientKey`), scoring and the digest use `OpenAIModel`.
- The chat client uses the OpenAI Responses API with `store=false` and encrypted reasoning (ADR-0011). Chat Completions rejects tools together with reasoning, and a stored response makes the agent framework refuse our own history. `ChatModelRequestTests` checks the real request; keep it green when the client or the agent changes.

## Settings from Telegram (ADR-0012)

- A parameter the bot may change goes into `SettingsCatalog` (ranges come from the `Range` attributes); secrets, schedules and infrastructure values never do.
- A job reads `IEffectiveOptions.GetAsync` once at the start of a run and passes the snapshot down; never hold `AgentOptions` in a singleton field. Secrets and infrastructure values keep `IOptions<AgentOptions>`.
- Every write to `settings/overrides.json` goes through `SettingsEditor` (ETag, up to 3 retries, validated as a whole); reset is never refused. A button's `callback_data` is at most 64 bytes and carries the target value, not a step.
- The menu answers only the bound private chat and records the ids of the updates it claimed (the last 30, a set, never a watermark: ids can restart and arrive out of order); a resent one is claimed and ignored, an ordinary chat question is never recorded. A blob that cannot be read is never overwritten except by "reset all". Models are chosen from a fixed list, the model is set per request by `ModelSelectingChatClient`.

- The chat agent changes settings only through `SettingsTools` (ADR-0013): they build a plan of `SettingChange` and never write. The plan is stored and shown by code (`SettingsProposalSender`, `SettingsProposalMessage`), applied only by the button (`SettingsEditor.ApplyProposalAsync`, one checked write). Never put model text into that message, never add an apply tool.

## Data and queues

- `run-now` messages start jobs only while `ManualRunsEnabled` is true (off by default in Azure) and are not retried: a failure is logged, not rethrown.
- The `Posts` table is partitioned by day (`PartitionKey` = date). Changing the `PostEntity` schema affects data that is already stored: make new fields optional.
- A `to-score` queue message is processed idempotently: a redelivery must not create duplicates.
- Model calls go through `IChatClient` with strict schemas built from C# types (`ChatClientExtensions.AskAsync`). Post text is data, not instructions.
- The digest model's entries refer to posts by `id` and carry only text; the channel, date and link come from the stored post (`DigestReport`, `DigestMessage`). The digest is several messages,
  and the posts are marked as sent after the last one (ADR-0009).
- A Telegram send is a POST and is never retried by the HTTP pipeline (`NotificationsServices.ConfigureTelegramResilience`): a repeat after a timeout duplicates a delivered message. Log the length and time of a send, never its text.

## Do not forget

- A new NuGet package: version in `Directory.Packages.props`, package in `.csproj` without a version.
- A change in a function's behavior needs a test in `tests/PostAgent.Tests/`, in the matching folder.
