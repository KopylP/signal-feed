# Edit prompt sections and keep a reader profile through the chat

- Status: Accepted
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

The **reader profile** is a separate plain-text blob, `settings/profile.txt`, written with an ETag. The chat adds it to its instructions inside `<about_me>` (escaped, as data) for every question.
It is changed through the same reviewed edit (`propose_profile_edit`), in a plan of its own: it is written after the proposal is taken out, so a tap never applies it twice.

## Options considered

- **Edit whole prompt files from the bot**: simplest to build, but one wrong edit breaks the strict schema or removes the data rules, and a 600-word text does not fit a Telegram message. Rejected.
- **Let the agent propose whole new section texts**: shorter dialogue, but the owner cannot see what changed in a long text. A fragment replacement shows the exact change. Rejected.
- **Keep the profile in `overrides.json`**: one write for everything, but "reset all" and the settings blob would carry personal text. A separate blob keeps it out of the settings. Chosen: separate.
- **Chosen**: marked sections, fragment edits confirmed by the owner, profile in its own blob.

## Consequences

The base instructions (schemas, limits, the data rules, tool names) cannot be changed from the bot, only from the repository. The agent can only follow the owner's own message: text in a post never reaches the edit tools by design, and the confirmation is built from the stored edit.
An edited prompt is still model input: a section that makes the model score everything 10 is possible, and "reset" or an edit back fixes it. Reset-all returns the prompts to the shipped text but leaves the profile.
A change applies to the next model call (the provider caches the blob for 30 seconds). New markers in a prompt file are a code change, and `PromptTemplateTests` keeps the safety rules outside the sections.
The profile is personal data in a blob (never in the repository). Revisit if the profile should also reach scoring and the digest.

## Updates

<!-- Append dated notes only (YYYY-MM-DD: note). Never rewrite the sections above after acceptance. -->
