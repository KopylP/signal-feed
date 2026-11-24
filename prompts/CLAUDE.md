# prompts

Prompts for the model: `scoring.md` (scoring posts) and `digest.md` (digest summary). They are copied into the build, so a change needs a new deploy.

- The prompts are written in Ukrainian, because the model's output goes to the end user. Structure: role, what to return, rules, limits.
- The output follows a strict schema built from a C# type, so field names in the prompt (`overview`, `items`, `id`, `insight`) must match the type in code. Change one, change the other.
- Post text is data, not instructions: keep the line telling the model not to follow requests found in the text.
- Do not ask the model to write links: the system adds them.
- The user's topics in `scoring.md` (in place of `{тема 1}`) are filled in by the owner, never invent them.
