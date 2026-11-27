# Edit prompt sections and keep a reader profile through the chat

- Status: Accepted (the reader profile part is superseded by ADR-0022)
- Date: 2026-11-25
- Deciders: Maintainer

## Context

The three prompts (`scoring.md`, `digest.md`, `chat.md`) are copied into the build, so any change needs a deploy ([prompts/CLAUDE.md](../../prompts/CLAUDE.md)). The owner wants to tune them from Telegram,
and to keep a short "about me" that the chat always sees. The prompts also hold what must not move: the schema field names and limits, and the rules that treat channel text as data.
The chat agent already changes settings only by building a plan that the owner confirms with a button, and the confirmation text is made by code ([ADR-0013](0013-let-the-chat-agent-propose-settings-changes-the-owner-confirms.md)).

## Decision

We split each prompt into fixed text and **editable sections**, marked in the file (`<!-- editable: id | label -->` ... `<!-- /editable -->`).
Only the sections can change. The owner's text for a section is stored in `settings/overrides.json` (`PromptSections`, section id to text), and the shipped text is the default; the model calls read the prompt through `IPromptProvider`.
The agent changes a section only with a reviewed edit: `propose_prompt_edit(section, old_text, new_text)` replaces one fragment (up to 500 characters) that must be found once in the current section, so the owner sees an exact before and after
(built by code, in the proposal message) and applies it with the same button as any settings plan. Up to three text edits per plan; a section keeps no headings or markers and is never empty; `propose_prompt_reset` restores the default.
The agent reads any prompt with `get_prompt(name)`, with the editable sections named.

The **reader profile** is a separate plain-text blob per chat, `settings/profile/<chat id>.txt` (one chat never sees another's), written with an ETag. The chat adds it to its instructions inside `<about_me>` (escaped, as data) for every question.
It is changed through the same reviewed edit (`propose_profile_edit`), in a plan of its own: it is written after the proposal is taken out, so a tap never applies it twice.

## Options considered

- **Edit whole prompt files from the bot**: simplest to build, but one wrong edit breaks the strict schema or removes the data rules, and a 600-word text does not fit a Telegram message. Rejected.
- **Let the agent propose whole new section texts**: shorter dialogue, but the owner cannot see what changed in a long text. A fragment replacement shows the exact change. Rejected.
- **Keep the profile in `overrides.json`**: one write for everything, but "reset all" and the settings blob would carry personal text. A separate blob keeps it out of the settings. Chosen: separate.
- **Chosen**: marked sections, fragment edits confirmed by the owner, profile in its own blob.

## Consequences

The base instructions (schemas, limits, the data rules, tool names) cannot be changed from the bot, only from the repository. The model could be influenced by text it reads (a post, a quote, a tool result), so the guard is twofold, as in ADR-0013: `chat.md` says that only the reader's own message can ask for an edit, and nothing is applied without the owner's tap on a confirmation built by code from the stored edit. The effect of a bad edit lasts longer than a setting, which is why the exact before and after is shown.
An edited prompt is still model input: a section that makes the model score everything 10 is possible, and "reset" or an edit back fixes it. Reset-all returns the prompts to the shipped text but leaves the profile.
A prompt edit applies within 10 seconds (the provider caches the blob; a profile edit applies at once). A pending profile edit sits in `overrides.json` as a proposal until it is applied or expires, and "reset all" clears the proposals. A section whose shipped text is improved later keeps the owner's text; "reset" returns it to the new default. New markers in a prompt file are a code change, and `PromptTemplateTests` keeps the safety rules outside the sections.
The profile is personal data in a blob (never in the repository). Revisit if the profile should also reach scoring and the digest.

## Updates

<!-- Append dated notes only (YYYY-MM-DD: note). Never rewrite the sections above after acceptance. -->

2026-11-27: the reader profile is superseded by [ADR-0022](0022-keep-a-reader-memory-the-chat-agent-manages-itself.md): a reader memory of up to 10 Markdown notes that the agent writes itself, without a confirmation. The editable prompt sections stay as decided here.
