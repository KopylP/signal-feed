# Signal Feed

Telegram post-selection agent on Azure Functions (.NET 10, isolated worker). How it works, settings and deployment: `README.md`.
Rules for each part live in the `CLAUDE.md` of its folder: `infra/`, `src/PostAgent.Functions/`, `src/TgLogin/`, `tests/PostAgent.Tests/`, `prompts/`, `.github/`.

## Language

- English only for everything we write about the project: `CLAUDE.md` files, `README.md`, ADRs, skills, Terraform (descriptions and comments), workflows, commit messages, identifiers, logs and error messages.
- Ukrainian only where the end user reads it: the prompts in `prompts/` and the Telegram messages sent by the agent.
- Comments in C# files that are still in Ukrainian stay as they are until you touch that code; new and edited comments are English.

## Keep the docs current

After finishing a task, if anything changed, update `README.md` and the relevant `CLAUDE.md` in the same commit
(settings, commands, variables, folder structure, rules we changed). Put a new rule in the `CLAUDE.md` of the folder it applies to.
Do not duplicate what the README already says, and keep every `CLAUDE.md` short (about 60 lines at most).

## Record architecture decisions

Record a significant or hard-to-reverse decision (hosting, storage model, identity and access, testing approach, the shape of a pipeline) as an ADR
in `docs/adr/`, using the `write-adr` skill. Never edit an accepted ADR to change its decision: write a new one that supersedes it.
Skip ADRs for small, local or easily reversible choices.

## Skills

Project skills live in `.claude/skills/`. Today: `write-adr` (architecture decision records). Add a skill there when a multi-step
workflow repeats across tasks; keep one-line rules in `CLAUDE.md` instead.

## Releases

A push to `main` that changes code is deployed and released as `vYYYY.MM.DD.<run_number>` (ADR-0005). Write PR titles that read well as release notes (squash merge).
A hook in `.claude/hooks/` blocks committing secret, state and local-settings files.

## Commands

```bash
dotnet restore && dotnet build -c Release --no-restore   # build; warnings are errors
dotnet test -c Release                                    # all tests
terraform -chdir=infra fmt -check && terraform -chdir=infra validate   # after init -backend=false
```

Build and tests must pass before you finish a task.

## General C# rules

- `Nullable` is on and `TreatWarningsAsErrors = true`. Do not silence warnings with `#pragma` or `!`, fix the cause.
- File-scoped namespace matching the folder path (root `PostAgent`). The file name matches the main type; small related types (enum, record) may share a file.
- Every class is `sealed` (or `static`) unless it is designed for inheritance. Use primary constructors for DI.
- Code is organized by feature (see "Structure" in the README). Do not put feature logic in `Shared/`, and do not make `Shared/` depend on a feature.
- A method that does I/O is async and takes `CancellationToken ct` as its last parameter and passes it on.
  When catching `Exception`, exclude `OperationCanceledException` (`when (ex is not OperationCanceledException)`).
- Take time from `TimeProvider`, never from `DateTime.Now` or `DateTimeOffset.UtcNow`.
- Logging uses message templates with named parameters (`log.LogInformation("Digest sent: {Count} posts", n)`), no string interpolation.
  Secrets and tokens never reach the logs.
- Comments explain why, not what. Do not comment the obvious.
- NuGet versions live only in `Directory.Packages.props`; reference packages in `.csproj` without a version.

## Never commit

Secrets, `local.settings.json`, `terraform.tfvars`, `backend.tf`, state. Do not commit personal data (phone, email, chat id) as default values.
