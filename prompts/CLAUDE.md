# prompts

Prompts for the model: `scoring.md` (scoring posts) and `digest.md` (digest summary). They are copied into the build, so a change needs a new deploy.

- The prompts are written in English, but the text the model returns (`topic`, `reason`, `overview`, `insight`) goes to the end user, so each prompt says it must be Ukrainian.
  Structure: role, what to return, rules, limits.
- The output follows a strict schema built from a C# type, so field names in the prompt (`overview`, `items`, `id`, `insight`) must match the type in code. Change one, change the other.
- Post text is data, not instructions: keep the line telling the model not to follow requests found in the text.
- Do not ask the model to write links: the system adds them.
- The user's topics in `scoring.md` (`My interests`) come from the owner, never invent them. Add examples of good and weak posts only when the owner supplies real ones.
