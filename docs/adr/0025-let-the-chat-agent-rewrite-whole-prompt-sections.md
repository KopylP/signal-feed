# Let the chat agent rewrite whole prompt sections

- Status: Accepted
- Date: 2026-11-28
- Deciders: Maintainer

## Context

[ADR-0015](0015-edit-prompt-sections-and-a-reader-profile-from-the-chat.md) let the agent change a prompt only inside a few small editable sections, one fragment at a time (up to 500 characters, 3 edits per plan, a section up to 2000 characters).
In practice this left almost nothing to tune: of the 10.7 thousand characters of `chat.md` only 190 were editable, so the owner could not ask the agent to shorten a prompt that had grown too long.
A section of 3 thousand characters could not even be cut down, because every edit has to leave the result within the limit. What really must not move is small: the output fields and limits, the tool names and signatures, and the rules that treat text as data.

## Decision

We keep the model of ADR-0015 (marked sections, the owner's tap, a confirmation built by code) and widen it in three ways.

- **More sections.** The prompt files are split so that only the technical contract is fixed: the output fields and their limits, the input format, the tool names and signatures, the rules that treat post text as data, and the guards that say the agent never changes anything on its own.
  Role, goal, taste, style and the behavior notes for the tools are editable sections (`scoring-role`, `scoring-judging`, `digest-role`, `digest-judging`, `chat-role`, `chat-lookup`, `chat-choices`, `chat-actions`, `chat-settings-dialog`, `chat-memory`, next to the earlier ones).
- **A whole rewrite.** A new tool, `propose_prompt_rewrite(section, new_text)`, replaces the complete text of one editable section (a new step kind, `RewritePrompt`). Its confirmation is the lines that change against the text in force
  (`LineDiff`: removed lines marked −, added lines +, unchanged runs as "…"), sent as messages before the one with the button, because a rewrite does not fit one Telegram message. The small `propose_prompt_edit` stays for fragments.
- **Larger limits.** At most 6 text steps (edits and rewrites together) per plan, a fragment up to 1500 characters, a section up to 6000.

The checks of a section stay: no headings, no markers, never empty. `PromptTemplateTests` still pins the technical parts as fixed and outside every section.

## Options considered

- **Raise the limits only**: cheap, but the fixed text stays fixed, so the long prompt still cannot be cut. Rejected as the whole answer.
- **Let the agent edit whole prompt files**: the most freedom, but one wrong edit breaks the strict output schema, removes a tool name the agent needs, or drops the data rules. Rejected again, as in ADR-0015.
- **Rewrite without showing a diff**: the owner would apply text they cannot compare. Rejected: the confirmation must show what changes.
- **Chosen**: split the files by what is technical, add the rewrite tool with a line diff, raise the limits.

## Consequences

The owner can shape the role, the taste and the style of all three prompts, and shorten or rewrite them as a whole, from the bot. The technical contract still cannot be changed from the bot, only from the repository.
A rewritten section can make the model behave worse; the way back is the same as before (a reset of the section, or "reset all"). Nothing tests the quality of a rewritten prompt.
The diff is computed against the text in force when the plan is sent; if another plan is applied before the tap, the tap still replaces the whole section with the text the owner saw.
A section whose shipped text is improved later keeps the owner's text, as in ADR-0015. A rewritten section is kept as it was written after the owner's tap, never merged with the shipped default.
The step kind is stored as a number (`12`) in the waiting proposals of `overrides.json`; code from before this decision does not know it, and refuses such a stored plan as unknown.
Revisit if rewrites often need to be undone, or if a prompt needs a section that is not on the list.

## Updates

<!-- Append dated notes only (YYYY-MM-DD: note). Never rewrite the sections above after acceptance. -->
