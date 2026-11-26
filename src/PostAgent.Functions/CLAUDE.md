# src/PostAgent.Functions: the Azure Functions host

Only the entry points live here: `Program.cs`, `host.json` and the classes with `[Function]` (a timer, queue or HTTP trigger). Everything else is in `src/PostAgent.Core`,
whose `CLAUDE.md` holds the feature rules. General C# rules are in the root `CLAUDE.md`; the project layout is in `docs/developer-guide/code-structure.md`.

## Rules

- A function stays thin: it takes its dependencies through the constructor, parses or checks its trigger input, calls one class of Core (`DailySummaryJob`, `CleanupRunner`, `OnDemandDigest`) and catches only what it can handle.
  Logic, I/O, formatting and settings belong in Core, never here. `ArchitectureTests` fails when a `[Function]` class is in Core or Core references the Functions host.
- A function class sits in the folder of its feature (`Chat/ChatFunction.cs`, `Cleanup/CleanupFunction.cs`) and is named for what it does (`DailySummaryFunction`, not `TimerFunction`). The namespace is the feature's (`PostAgent.Chat`), as in Core.
- The worker finds functions only in this assembly, so a trigger cannot move to Core. Do not add a `[Function]` to Core and do not reference the Functions host packages from Core.
- A timer reads its schedule from an App Setting by `%Name%` (`%DailySummarySchedule%`); the setting is a Terraform variable with a default (`infra/CLAUDE.md`). A queue trigger names its queue with a constant from `StorageNames`, and uses `Connection = "AzureWebJobsStorage"`.
- A new function: add the class here, register what it needs in the feature's `XxxServices` in Core (`AddPostAgent` calls it), add a test through `Run` in the feature's folder of `tests/PostAgent.Tests`, and add it to `CompositionTests` (it must be creatable from the container).
- `local.settings.json` is local only and never committed (copy `local.settings.json.example`); the prompts in `prompts/` are copied into this project's output by the csproj.
