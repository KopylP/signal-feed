# CI/CD

**Audience:** developers and maintainers who work with the pipelines, releases and rollback.
Installing by hand without GitHub Actions: [installation](../user-guide/installation.md). Architecture decisions behind this: [ADR-0004](../adr/0004-run-terraform-from-a-manual-workflow.md), [ADR-0005](../adr/0005-release-every-deploy-with-a-build-counter-version.md), [ADR-0006](../adr/0006-fix-formatting-and-validate-terraform-in-the-build-job.md).

## Pipelines at a glance

| Workflow | Starts | Does |
|---|---|---|
| [`deploy.yml`](../../.github/workflows/deploy.yml) (`build-test-deploy`) | push to `main`, every PR, or by hand | Formats, builds and tests; from `main` also deploys the code and creates a release |
| [`infra.yml`](../../.github/workflows/infra.yml) (`infra`) | by hand only | `terraform plan`, and `apply` when the checkbox is set |

Code deploys are frequent and low risk, so they are automatic. Infrastructure changes are rare and can be destructive, so they are manual.

## Code pipeline (`deploy.yml`)

Workflow: [`.github/workflows/deploy.yml`](../../.github/workflows/deploy.yml). It builds and tests on a push to `main` and on every PR, and from `main` it also deploys (deploys never run at the same time) and then creates a release. It can also be started by hand (`workflow_dispatch`).

Pushes that only change `docs/**`, `README.md`, `CHANGELOG.md`, `CLAUDE.md` files, `.claude/**` or `infra/**` do not run it (the `prompts/*.md` files do, because they ship with the build).
Formatting follows [`.editorconfig`](../../.editorconfig). The build job of `deploy.yml` runs `dotnet format` first: on a pull request from this repository it (and `terraform fmt`) commits any fix to the PR branch as `github-actions[bot]` and builds the fixed code; on `main`, forks and Dependabot it only verifies. Run `dotnet format` locally to fix it before you push.

The same build job runs `terraform validate` on `infra/` (no backend, no Azure sign-in), so a broken Terraform change blocks the deploy too. Dependabot opens weekly grouped updates for NuGet, GitHub Actions and Terraform.

### One-time OIDC setup

The pipeline signs in to Azure without passwords:

```bash
APP_ID=$(az ad app create --display-name signal-feed-deploy --query appId -o tsv)
az ad sp create --id $APP_ID
az ad app federated-credential create --id $APP_ID --parameters '{
  "name": "github-main",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:<github-owner>/<repo>:ref:refs/heads/main",
  "audiences": ["api://AzureADTokenExchange"]
}'
az role assignment create --assignee $APP_ID --role Contributor \
  --scope $(az functionapp show -g <rg> -n <function-app> --query id -o tsv)
```

In the GitHub repository (Settings → Secrets and variables → Actions → Variables) set:
`AZURE_CLIENT_ID` (this is `$APP_ID`), `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_FUNCTIONAPP_NAME`.

## Infrastructure pipeline (`infra.yml`)

The workflow [`.github/workflows/infra.yml`](../../.github/workflows/infra.yml) runs only by hand (Actions → infra → Run workflow).
Without the `apply` checkbox it only runs `terraform plan` (visible in the run summary); with the checkbox it also runs `terraform apply`.
If the infrastructure does not exist, it is created; if the code in `infra/` changed, it is updated. The application code is deployed by the separate workflow
`build-test-deploy` after a push to `main`.
After an apply the workflow checks that `AzureWebJobsStorage` and `DEPLOYMENT_STORAGE_CONNECTION_STRING` are absent from the app (names only): the `azurerm` provider injects them on every update of a Flex Consumption app, and `azapi_resource_action.app_settings` in `infra/function.tf` overwrites them (hashicorp/terraform-provider-azurerm#29149).

Because the federated credential accepts only `refs/heads/main`, the workflow can sign in to Azure only when run from `main`: merge an infrastructure change first (it deploys no code), then run `infra` from `main`.

### Bootstrap

The pipeline creates the resources itself, so its identity (App Registration) and the state storage are created **by hand once**:
they cannot be described in the same Terraform that they run.

```bash
# 1. State storage
az group create -n rg-tfstate -l westeurope
az storage account create -g rg-tfstate -n <unique-sttfstate-name> --sku Standard_LRS --allow-blob-public-access false
az storage container create --account-name <same-name> -n tfstate --auth-mode login

# 2. Pipeline identity (instead of the variant in the OIDC section above, with broader roles)
SUB=$(az account show --query id -o tsv)
APP_ID=$(az ad app create --display-name signal-feed-deploy --query appId -o tsv)
az ad sp create --id $APP_ID
az ad app federated-credential create --id $APP_ID --parameters '{
  "name": "github-main", "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:<github-owner>/<repo>:ref:refs/heads/main", "audiences": ["api://AzureADTokenExchange"]
}'
# create resources
az role assignment create --assignee $APP_ID --role Contributor --scope /subscriptions/$SUB
# grant roles to the function and to you (roles.tf); Contributor cannot do this
az role assignment create --assignee $APP_ID --role "Role Based Access Control Administrator" --scope /subscriptions/$SUB
# read and write the state
az role assignment create --assignee $APP_ID --role "Storage Blob Data Contributor" \
  --scope $(az storage account show -n <sttfstate-name> --query id -o tsv)
```

### GitHub Variables

Settings → Secrets and variables → Actions → Variables, all non-secret:

| Variable | Value |
|---|---|
| `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` | sign-in to Azure (OIDC) |
| `TFSTATE_RESOURCE_GROUP`, `TFSTATE_STORAGE_ACCOUNT` | state storage (`TFSTATE_CONTAINER`, default `tfstate`) |
| `TF_DEPLOYER_OBJECT_ID` | `deployer_object_id` |
| `TF_SOURCES` (optional), `TF_ALERT_SOURCES` | `sources` (default: the six SEO channels from `variables.tf`), `alert_sources` |
| `TF_COLLECTOR_SCHEDULE`, `TF_DIGEST_SCHEDULE`, `TF_ALERT_SCHEDULE`, `TF_CLEANUP_SCHEDULE` (optional) | CRON in UTC; default: the values in the settings table; `TF_APP_SETTINGS` overrides them |
| `TF_LOCATION`, `TF_NAME_PREFIX` (optional) | default `westeurope` and `signalfeed` |
| `TF_APP_SETTINGS` (optional) | JSON, for example `{"DigestSize":"6"}`; matches `app_settings` |

Personal data goes into **Secrets** (same page, Secrets tab), not Variables: this repository is public, and the values of Variables are not masked in the run logs. Terraform also marks these inputs `sensitive`, so the plan summary hides them.

| Secret | Value |
|---|---|
| `TF_BUDGET_ALERT_EMAIL` | `budget_alert_email` |
| `TF_TELEGRAM_API_ID` | `telegram_api_id` |
| `TF_TELEGRAM_CHAT_ID` (optional) | override of the paired chat; leave unset to bind the chat with `/start` |

The API keys (OpenAI, Telegram) are not needed in GitHub: as before, they go into Key Vault with `az keyvault secret set`.
After the first `apply`, copy `function_app_name` from the run summary into `AZURE_FUNCTIONAPP_NAME` for the code deploy.

## Releases and rollback

Every deploy from a push to `main` is a release: a `vYYYY.MM.DD.N-word` tag and a GitHub Release. `YYYY.MM.DD` is the UTC build date and `N` is GitHub's
built-in run counter of `deploy.yml`, which only grows, so versions never collide (numbers have gaps because pull request builds count too).
The number is baked into the build (`-p:Version`), so it matches the tag. No deploy (see [Deploy is off by default](#deploy-is-off-by-default)) means no release.
Details: [ADR-0005](../adr/0005-release-every-deploy-with-a-build-counter-version.md) and [ADR-0007](../adr/0007-name-each-release-with-a-changelog-word.md).

**The word and the changelog.** A pull request adds a section to [`CHANGELOG.md`](../../CHANGELOG.md) headed `## [word] - date` (the `write-changelog` skill).
On the push, the build job reads the word from the diff and builds the tag. After the deploy, the release job uses the section as the release notes, renames the heading to
`## [v2026.11.24.131-word] - date` and commits that to `main` as `github-actions[bot]` (the branch rules must allow it). A push that adds no section gets a plain `vYYYY.MM.DD.N` tag and generated notes.
The logic is in `.github/scripts/changelog.py`, tested by `python3 -m unittest discover -s .github/scripts`.

**Rollback.** Actions → build-test-deploy → Run workflow, and enter the earlier release tag, for example `v2026.11.20.120` or `v2026.11.24.131-falcon`. The workflow rebuilds that tag, tests it and deploys it,
and creates no new release. Or revert the commit through a pull request, which deploys the previous code as a new release. A rollback restores code only: infrastructure,
App Settings, secrets and stored data stay as they are, and a renamed or removed required setting can stop an older version from starting.

The repository must allow Actions to create releases: the release job asks for `contents: write`, so Settings → Actions → General → Workflow permissions must not block it.

### Deploy is off by default

Without the `DEPLOY_ENABLED` variable set to `true`, the workflow only builds and tests, and the deploy and release steps
are skipped. When you are ready to deploy, set this variable in the same place as the rest.
