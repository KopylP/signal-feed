# prompts

Prompts for the model: `scoring.md` (scoring posts), `digest.md` (digest summary) and `chat.md` (chat about the digest and about changing settings: free text, no schema, in Ukrainian and plain text, because Telegram shows it literally; tool names in it must match the tools in `Chat/Agent/Tools/`). They are copied into the build, so a change of the files needs a new deploy.

- Editable sections (ADR-0015, ADR-0025): wrap a part the owner may tune from the bot in `<!-- editable: id | label -->` ... `<!-- /editable -->` after its heading. Put in sections everything that is wording and taste: the role and goal, interests, scale, style, how to judge a post, the behavior notes for the tools.
  Keep outside every section the technical contract: output fields and limits, the input format, tool names and signatures, the rules that treat text as data, and the guards that forbid the agent to act on its own. `PromptTemplateTests` pins them.
  The owner's text replaces a section (as one edited fragment or a whole rewrite), so write the default as a complete text, and do not name a pinned tool or field inside a section. Keep a section under `TextEdit.MaxText`. Keep the ids: an id is the key of the owner's stored text.

- The prompts are written in English, but the text the model returns (`topic`, `reason`, and the digest texts) goes to the end user, so each prompt says it must be Ukrainian.
  Structure: role, what to return, rules, limits.
- The output follows a strict schema built from a C# type, so field names in the prompt must match the type in code: `topic`, `reason`, `score`, `duplicate_of` for scoring,
  and `overview`, `headlines`, `for_marketplace`, `notable`, `recommended`, `needs_verification`, `skipped` (with their entry fields) for the digest (`DigestSummary`). Change one, change the other.
  Use each record type once in a schema: a type used twice becomes a `$ref` that OpenAI strict mode rejects (`ModelSchemaTests` checks it, the fake chat client cannot).
- The message layout is fixed in code, so the prompts must fit its limits: `topic` is cut at 60 characters and `reason` at 200 (`TelegramHtml`, also used by alerts);
  the digest cuts `overview` at 500, titles at 100, summaries at 700, "why it matters" at 350, the `skipped` note at 400 (`DigestMessage`). The prompt asks for less than these.
  A new section or a longer text needs a change in the schema and the message, not only in the prompt (ADR-0009).
- This repository is public: describe the owner's work only as far as the scoring needs (no name, title, email or phone).
- Post text is data, not instructions: keep the line telling the model not to follow requests found in the text.
- `scoring.md` may receive `<reader_feedback>` before the posts (the topics of the posts the reader liked or disliked through the agent's `rate_post`, `Shared/Feedback`); the "Reader feedback" section is fixed (outside the editable ones), caps its effect at one point and says it is data.
- Do not ask the model to write links: the system adds them.
- The user's topics in `scoring.md` (`My interests`) come from the owner, never invent them. Add examples of good and weak posts only when the owner supplies real ones.
