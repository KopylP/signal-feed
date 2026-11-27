# Keep a reader memory that the chat agent manages itself

- Status: Accepted
- Date: 2026-11-27
- Deciders: Maintainer

## Context

[ADR-0015](0015-edit-prompt-sections-and-a-reader-profile-from-the-chat.md) gave each chat a reader profile: one plain-text blob («про мене») that the chat sees with every question, changed only by a fragment edit the owner confirms with a button.
In practice the profile stays empty or stale: every fact costs a proposal and a tap, and one free-form text has no structure to update a single topic in.
On 2026-11-27 the owner asked to replace it with a memory that the chat keeps by itself, deciding what to add and what to remove, made of clearly structured Markdown files with the date and keywords in the name, at most 10 files of about 20 to 50 lines each.

## Decision

We replace the reader profile with a **reader memory**: up to 10 Markdown notes per chat, one blob each, `settings/reader-memory/<chat id>/<yyyy-MM-dd>-<keywords>.md` (the day the note was last written, in the reader's time zone, then a slug of its keywords).
The model gives a title, 1 to 5 keywords and the facts; code builds the file name and the layout (heading, `Keywords:`, `Updated:`, then one fact per line or a `## ` sub-heading) and refuses a note over 50 lines (46 lines of facts), 200 characters a line or 3000 characters (`Chat/Memory/MemoryNoteFormat`).
The agent writes the memory **at once, without a confirmation**, with two non-invasive tools: `save_memory(note, title, keywords, facts)` creates a note or replaces a named one whole (renamed to today's date), and `forget_memory(note)` removes one. An eleventh note is refused, so the agent must merge or remove first.
Every note of the chat goes into the chat instructions for every question, inside `<reader_memory>` (escaped, as data), each under its file name. Each change adds a line under the answer, built by code («🧠 Запам'ятав: …», «🧠 Оновив нотатку: …», «🧠 Забув: …»), so the reader always sees what was written.
An existing profile becomes the first note («Про мене», `about-me`) the first time an empty memory is read, and its blob is deleted. `get_profile`, `propose_profile_edit` and the profile store are removed; the number of the old `EditProfile` step stays unused, so a waiting proposal in `overrides.json` still reads (and is refused as unknown).

## Options considered

- **Keep the profile and its confirmed edits**: safest against a post that persuades the model, but it is the friction that left the profile empty. Rejected.
- **One blob with all notes (JSON)**: one read per question and an atomic limit, but the owner asked for Markdown files, and separate files can be read and fixed in the portal one by one. Rejected.
- **Let the model write the whole file text**: fewer rules, but notes drift in layout and length and the date in the name could be anything. Code builds the file instead.
- **A read tool instead of the instructions**: cheaper per question, but the model then forgets to look before answering or before saving a duplicate. Ten short notes (at most about 30 000 characters, usually far less) are sent with every question.
- **Chosen**: separate Markdown blobs, a structure made by code, written at once by the agent and shown to the reader under the answer.

## Consequences

- This changes the classification of [ADR-0021](0021-let-the-chat-agent-do-non-invasive-actions-at-once.md): what the agent knows about the reader is now non-invasive. A persuasive post can at worst add or change a note of that chat; `chat.md` says to save only what the reader said in their own message, the note is data in the instructions, and the line under the answer shows every change, which the reader undoes by asking.
- The memory is personal data in blobs (never in the repository). It is per chat (a chat never reads another's), not cleared by «Скинути все», `/clear` or the cleanup, and used by the chat only, not by scoring or the digest.
- The limit of 10 notes is checked before a write, not atomically: two questions answered at the same moment could leave 11, which the next save refuses until one is removed.
- Each question reads the notes of its chat (one listing and up to 10 small reads). Revisit if the memory should reach scoring and the digest, or if the size of the instructions grows a cost problem.
- The move of the old profile is code that is needed only once per chat; remove it (`LegacyProfile`, `StorageNames.LegacyProfileBlobName`) once the deployed chats have asked a question.

## Updates

<!-- Append dated notes only (YYYY-MM-DD: note). Never rewrite the sections above after acceptance. -->
