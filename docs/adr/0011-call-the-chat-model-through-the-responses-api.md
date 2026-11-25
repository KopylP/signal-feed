# Call the chat model through the Responses API without stored output

- Status: Accepted
- Date: 2026-11-25
- Deciders: Maintainer

## Context

The chat (ADR-0010) shipped on Chat Completions with `gpt-6.1-terra`. Its first real question failed: OpenAI answers HTTP 400 to function tools combined with reasoning on `/v1/chat/completions`
("To use function tools, use /v1/responses or set reasoning_effort to 'none'"). The maintainer wants reasoning (`medium`), so the chat needs the tools and the reasoning together.
The fake chat client in the tests could not show this; only the real client over a recording transport can. The Responses client in the OpenAI SDK is marked experimental (`OPENAI001`).
The conversation is ours (one blob, ADR-0010): when OpenAI stores the response, it returns an id that the agent framework takes for server-side history and refuses to combine with our own.

## Decision

We build the chat client on the Responses API (`AiServices.CreateChatClient`) with `StoredOutputEnabled = false` and the encrypted reasoning included, so that reasoning items travel inside our own session and are sent back after a tool call.
The effort is the setting `ChatReasoningEffort` (default `medium`), and the output limit `ChatMaxOutputTokens` is 4000 because hidden reasoning counts against it.
The project suppresses `OPENAI001` in `PostAgent.Functions.csproj` only, for this one experimental client.
Scoring and the digest stay on Chat Completions with `OpenAIModel`.

## Options considered

- **Chat Completions with `reasoning_effort=none`**: one line, stable API, but no reasoning, which is the reason for the stronger model.
- **Responses API with stored output**: OpenAI keeps the history, but the agent framework rejects a response id next to our own history provider, and the conversation would live in two places.
- **Responses API without stored output (chosen)**: reasoning and tools work and the blob stays the only store; it costs an experimental SDK type and a request shape that is only checked offline.

## Consequences

`ChatModelRequestTests` runs the production client over a recording transport and checks the endpoint, the tools, the effort, `store=false` and that the encrypted reasoning is sent again after a tool call and after the conversation went through the blob.
It cannot prove what OpenAI does with it: the first questions after a deploy are the real test, and `ChatReasoningEffort=none` is the quick fallback. The experimental type can change in a later SDK version: revisit when the OpenAI package is updated.

## Updates

<!-- Append dated notes only (YYYY-MM-DD: note). Never rewrite the sections above after acceptance. -->
