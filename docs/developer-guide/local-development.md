# Local development

**Audience:** developers who want to build, run and test the agent on their own machine, without Azure.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) (`npm i -g azurite`), a local emulator of Azure Storage
- Terraform (version in `.terraform-version`) only if you touch `infra/`

You still need real credentials for Telegram and OpenAI: see the [overview](../user-guide/overview.md#what-you-need).

## Run the agent

```bash
azurite --silent --location .azurite &
cp src/PostAgent.Functions/local.settings.json.example src/PostAgent.Functions/local.settings.json
# fill in the placeholders: OpenAI key, bot token, Telegram data

# one-off Telegram sign-in (the session goes to Azurite)
export TELEGRAM_API_ID=... TELEGRAM_API_HASH=... TELEGRAM_PHONE=+380...
dotnet run --project src/TgLogin -- "UseDevelopmentStorage=true"

cd src/PostAgent.Functions && func start
```

Locally the tables, queue and container are created automatically. To see a result right away, trigger a timer by hand:

```bash
curl -X POST http://localhost:7071/admin/functions/CollectorFunction -H "Content-Type: application/json" -d "{}"
```

`local.settings.json` is in `.gitignore`. Never commit it, and never put real values in the `.example` file.

## Build, test, format

Run these before you push. They are the same checks the pipeline runs, and warnings are errors.

```bash
dotnet restore && dotnet build -c Release --no-restore
dotnet test -c Release
dotnet format --verify-no-changes --no-restore            # drop --verify-no-changes to fix
terraform -chdir=infra fmt -check && terraform -chdir=infra validate   # after: terraform -chdir=infra init -backend=false
```

Storage code (`PostRepository`) is not covered by `dotnet test`. Check it by hand against Azurite after changing it.

## Before opening a pull request

Use the `check-before-pr` skill (`.claude/skills/`). Keep the documentation current in the same commit, add a [`CHANGELOG.md`](../../CHANGELOG.md) entry when behavior, a contract or a setting changes (`write-changelog` skill), and record hard-to-reverse decisions as an [ADR](../adr/README.md).
What happens after the merge: [CI/CD](ci-cd.md).
