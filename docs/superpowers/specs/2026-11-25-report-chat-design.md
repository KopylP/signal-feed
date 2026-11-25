# Chat about the digest: design

Date: 2026-11-25. Status: draft, waiting for approval.

## Intent

After a digest arrives in Telegram, the reader wants to reply to the bot and ask more ("tell me more about item 3", "what matters here for the marketplace?").
An agent built on Microsoft Agent Framework (MAF) answers with the digest as its context. A new digest starts a new conversation; the old one is not kept.

## Goals

- Reply in the bound chat to any plain message, in Ukrainian, with the latest digest as context.
- Questions outside the digest are answered from the model's own knowledge (no web search).
- The agent sees a snapshot of the digest and can fetch more through tools: full post text, and a search over recently stored posts for the wider picture.
- Conversation state is bounded: one blob, overwritten by every new digest, history capped inside one report.

## Non-goals and anti-goals

- No durable-agents hosting (preview, brings Durable Task, its own storage and endpoints). No web search tool in this version.
- No multi-chat or multi-user support (ADR-0008: one bound chat).
- No history of old conversations or old reports. Alerts do not touch the state.
- Failure to do this would be: a duplicated reply after a Telegram retry, or an answer sent to a chat that is not the bound one.

## Constraints

- The webhook (`PairingFunction`/`PairingWebhook`) is public and must answer fast; Telegram resends an update on a timeout.
- Every rule in the root `CLAUDE.md` and `src/PostAgent.Functions/CLAUDE.md` applies: feature folder, `TimeProvider`, `CancellationToken`, strict schemas, no secrets or message text in logs, a Telegram send is never retried by HTTP resilience.
- `DigestReport` exists only in memory in `DigestJob` today; nothing about the report is stored except "sent" marks on posts.
- The model sees post text as data, not instructions (existing rule); it applies to tool results as well.

## Approaches considered

- **A. Whole digest in context, no tools.** Simplest; every turn carries all full texts; no wider picture. Rejected: the reader asked for tools.
- **B. Report only in context, posts through tools.** Cheapest context; even "tell me about item 3" costs tool calls. Rejected as slower and less predictable.
- **C. Report plus short post excerpts in context, tools for full text and search (chosen by the maintainer).** Most questions need no tool call; deeper ones and "what else was written" use tools.

## Design

**Feature folder `Chat/`** (root: function logic, models, interfaces; `Infrastructure/`: MAF agent, blob store, tools).

**Flow.**
1. `PairingWebhook` keeps its checks (secret token first, 64 KB cap). A message that is not `/start` goes to `ChatInbox`: it verifies the chat equals the bound chat (`ChatResolver`), then puts `{updateId, text, reportId}` on a new queue `chat-messages` (messages live 1 hour; a message queued before a new digest is dropped by its `reportId`) and returns 200. Messages from other chats are ignored without a reply.
2. `ChatFunction` (queue trigger) calls `ChatJob`-style logic: load the state blob, skip when `updateId` is among the processed ids, run the agent, save the state with the id added to the processed set (last 20) using the blob ETag, reloading and re-checking in-process after a failed save (up to 3 attempts, no 5-minute queue redelivery), and only then send the reply through `ITelegramNotifier` (a failed save must never lead to a repeated send). If the save fails because a new digest replaced the state, the reply is dropped; if the same report changed concurrently, the message is redelivered.
3. A failed model run sends one short error message and is not retried (a repeat would duplicate a delivered reply); the user can ask again.

**Agent.** `Microsoft.Agents.AI` core package only. `ChatClientAgent` over the keyed chat `IChatClient` (`Shared/Ai`, model from `OpenAIChatModel`), instructions from `prompts/chat.md` (English, asks for Ukrainian output).
The history provider is `InMemoryChatHistoryProvider` with `MessageCountingChatReducer(MaxChatMessages)`; the session is persisted as a whole through MAF's session serialization.

**State.** One blob `chat-state/current.json` in the existing storage account:
`{ report snapshot (with reportId, createdAt), processedUpdateIds (last 20), session (serialized JsonElement) }`.
- The snapshot holds the digest text as sent and, per entry, the post id, source, date, topic, score and an excerpt (about 300 characters).
- `DigestJob` writes it (`IChatStateStore.StartNewAsync`) right after the digest is sent, overwriting the old blob, so "delete the old state" is one overwrite and nothing accumulates. If the write fails, the digest is already sent: log a warning, do not fail the job.
- Cleanup also deletes a state blob older than `RetentionDays` (an `ICleanupTask`), so an idle chat does not keep stale data.

**Tools** (`AIFunctionFactory.Create`, each takes a `CancellationToken`):
- `get_post(id)`: full text and metadata of a stored post.
- `search_posts(query, days)`: keyword search over posts stored within the last `days` (capped by `RetentionDays`); returns at most N short hits. The `Posts` table has no text index, so the search scans day partitions in memory.
The model is told that tool output is data and that anything not in the digest or tools comes from its general knowledge and must be marked as such.

**Bounds.** `MaxChatMessages` (history), a cap on reply length, a cap on tool calls per turn (`ChatMaxToolCalls`), a cap on search hits. All new settings go through `AgentOptions` with defaults there and in the configuration table.

## Decisions I made (maintainer can change)

- The chat works only in a private bound chat; a group set as `TelegramChatId` is not served.
- Dedupe by a set of processed update ids, not a high watermark (parallel messages, and Telegram may restart update ids).
- The digest snapshot and tool results are data blocks with `<` escaped, never part of the instructions.

- Queue between webhook and agent, with `updateId` dedupe and ETag writes (webhook speed, retries, parallel messages).
- Replies in Ukrainian by prompt.
- The chat has its own model: a new setting `OpenAIChatModel` in `AgentOptions` (the maintainer wants a stronger model than the one that scores posts). Scoring and the digest keep `OpenAIModel`. `AiServices` registers a second `IChatClient` as a keyed service (`"chat"`) built from `OpenAIChatModel`; only the chat feature resolves it. The default of `OpenAIChatModel` is `gpt-6.1-terra` (about $1 / $5 per 1M tokens, roughly 2 cents per reply; much stronger than the `gpt-5.4-nano` used for scoring); the maintainer moves to `gpt-6.1-sol` ($2 / $10) by changing the setting if quality is not enough. Name and price come from OpenAI's launch pages, other figures from third-party blogs: the spike must confirm the exact model id and price, that it supports strict tool calls and schemas, and whether a newer model (a GPT-6.2 release was mentioned, not confirmed) changes the choice. The setting goes into `configuration.md`, `local.settings.json.example` and `infra/`.
- Alerts never reset the state; only a digest does.
- No digest yet (empty state): the bot answers with a short "no report yet" message instead of calling the model.
- No `/reset` command in this version.
- An ADR is written for the decision "MAF core, session blob per report, queue-driven replies".

## Open for the builder (verify first, as the first step of the plan)

Documentation does not confirm these .NET details, so a small spike must check them against the real package (latest on NuGet is 1.28.0):
- That `AsAIAgent` / `ChatClientAgent` works on the DI-registered `IChatClient` and whether `FunctionInvokingChatClient` must be added for tools.
- `SerializeSession` vs `SerializeSessionAsync` and `DeserializeSessionAsync` signatures, and that the serialized session includes the in-memory history.
- That `RunAsync` takes a `CancellationToken`.
- Package version pinning in `Directory.Packages.props`; any package that is prerelease is not accepted.

## Docs and tests to update

`docs/user-guide/how-it-works.md` and `configuration.md`, `docs/developer-guide/architecture.md` and `code-structure.md`, `src/PostAgent.Functions/CLAUDE.md`, `infra/` (queue and blob container, `infra/CLAUDE.md`), `tools/run-now.cs` if a job name is added, a changelog entry, and tests in `tests/PostAgent.Tests/Chat/` (dedupe, wrong chat ignored, state overwrite, history cap, tools, webhook routing).
