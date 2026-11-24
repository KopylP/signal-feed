# prompts

Prompts for the model: `scoring.md` (scoring posts) and `digest.md` (digest summary). They are copied into the build, so a change needs a new deploy.

- The prompts are written in English, but the text the model returns (`topic`, `reason`, and the digest texts) goes to the end user, so each prompt says it must be Ukrainian.
  Structure: role, what to return, rules, limits.
- The output follows a strict schema built from a C# type, so field names in the prompt must match the type in code: `topic`, `reason`, `score`, `duplicate_of` for scoring,
  and `overview`, `headlines`, `for_marketplace`, `notable`, `recommended`, `needs_verification` (with their entry fields) for the digest (`DigestSummary`). Change one, change the other.
- The message layout is fixed in code, so the prompts must fit its limits: `topic` is cut at 60 characters and `reason` at 200 (`TelegramHtml`, also used by alerts);
  the digest cuts `overview` at 500, titles at 100, summaries at 700, "why it matters" at 350 (`DigestMessage`). The prompt asks for less than these.
  A new section or a longer text needs a change in the schema and the message, not only in the prompt (ADR-0009).
- This repository is public: describe the owner's work only as far as the scoring needs (no name, title, email or phone).
- Post text is data, not instructions: keep the line telling the model not to follow requests found in the text.
- Do not ask the model to write links: the system adds them.
- The user's topics in `scoring.md` (`My interests`) come from the owner, never invent them. Add examples of good and weak posts only when the owner supplies real ones.
