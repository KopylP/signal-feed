# Deployment

First deployment, GitHub Actions pipelines, releases and rollback. Settings: [configuration](configuration.md). Overview: [README](../README.md).

## Deployment

You need the Azure CLI (`az login`), Terraform ≥ 1.5 and an Azure subscription.

**Before you start.** Check that Flex Consumption is available in your region:
`az functionapp list-flexconsumption-locations -o table`, and change `location` if needed.

### 1. terraform apply

```bash
export ARM_SUBSCRIPTION_ID=$(az account show --query id -o tsv)
cd infra
cp terraform.tfvars.example terraform.tfvars   # fill in, notably deployer_object_id and budget_alert_email
terraform init
terraform apply
```

`deployer_object_id`: `az ad signed-in-user show --query id -o tsv`. State is local by default,
see `backend.tf.example` for remote state.

Terraform creates: a resource group, storage (containers `deployment-package` and `tg-session`, tables `ChannelState` and `Posts`,
queue `to-score`), a Key Vault (RBAC), Log Analytics (30 days, 0.1 GB a day), Application Insights, an FC1 plan and a function app
with a managed identity and roles, and a $5 monthly budget (alerts at 80% and 100%).

### 2. Secrets in Key Vault

Terraform does not create the secret values, so they never end up in the state:

```bash
KV=$(terraform output -raw key_vault_name)
read -rsp "OpenAI API key: " V && az keyvault secret set --vault-name $KV --name OpenAIApiKey --value "$V" >/dev/null; echo
read -rsp "Telegram bot token: " V && az keyvault secret set --vault-name $KV --name TelegramBotToken --value "$V" >/dev/null; echo
read -rsp "Telegram api_hash: " V && az keyvault secret set --vault-name $KV --name TelegramApiHash --value "$V" >/dev/null; echo
```

The app reads them through Key Vault references. If a reference is not picked up, check that the function's identity has the `Key Vault Secrets User` role
and restart the app (`az functionapp restart`). The app notices an unresolved reference by itself:
startup fails with an error that names the secret.

### 3. TgLogin (Telegram sign-in)

Use a separate account. `api_id` and `api_hash` come from [my.telegram.org](https://my.telegram.org). If a group cannot be
read, join it with this account.

```bash
export TELEGRAM_API_ID=... TELEGRAM_API_HASH=... TELEGRAM_PHONE=+380...
dotnet run --project src/TgLogin -- $(terraform -chdir=infra output -raw storage_account_name)
```

The program asks for the Telegram code and the 2FA password, then uploads the session to the `tg-session` container through `DefaultAzureCredential`
(your `az login`; the Storage Blob Data Contributor role has already been granted by Terraform). If the session expires, repeat this step.

### 4. Deploying the code

```bash
dotnet publish src/PostAgent.Functions -c Release -o publish
cd publish
func azure functionapp publish $(terraform -chdir=../infra output -raw function_app_name)
```

## GitHub Actions: build, test, deploy through OIDC

Workflow: [`.github/workflows/deploy.yml`](../.github/workflows/deploy.yml). It builds and tests on a push to `main` and on every PR, and from `main` it also deploys (deploys never run at the same time) and then creates a release. It can also be started by hand (`workflow_dispatch`).

Pushes that only change `docs/**`, `README.md`, `CLAUDE.md` files, `.claude/**` or `infra/**` do not run it (the `prompts/*.md` files do, because they ship with the build).
Formatting follows [`.editorconfig`](../.editorconfig). The build job of `deploy.yml` runs `dotnet format` first: on a pull request from this repository it (and `terraform fmt`) commits any fix to the PR branch as `github-actions[bot]` and builds the fixed code; on `main`, forks and Dependabot it only verifies. Run `dotnet format` locally to fix it before you push.

The same build job runs `terraform validate` on `infra/` (no backend, no Azure sign-in), so a broken Terraform change blocks the deploy too. Dependabot opens weekly grouped updates for NuGet, GitHub Actions and Terraform.

One-time OIDC setup (no passwords):

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

### Infrastructure from GitHub Actions (manual run)

The workflow [`.github/workflows/infra.yml`](../.github/workflows/infra.yml) runs only by hand (Actions → infra → Run workflow).
Without the `apply` checkbox it only runs `terraform plan` (visible in the run summary); with the checkbox it also runs `terraform apply`.
If the infrastructure does not exist, it is created; if the code in `infra/` changed, it is updated. The application code is deployed by the separate workflow
`build-test-deploy` after a push to `main`.

Because the federated credential accepts only `refs/heads/main`, the workflow can sign in to Azure only when run from `main`: merge an infrastructure change first (it deploys no code), then run `infra` from `main`.

The pipeline creates the resources itself, so its identity (App Registration) and the state storage are created **by hand once**:
they cannot be described in the same Terraform that they run.

```bash
# 1. State storage
az group create -n rg-tfstate -l westeurope
az storage account create -g rg-tfstate -n <unique-sttfstate-name> --sku Standard_LRS --allow-blob-public-access false
az storage container create --account-name <same-name> -n tfstate --auth-mode login

# 2. Pipeline identity (instead of the variant in the section above, with broader roles)
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

GitHub Variables (Settings → Secrets and variables → Actions → Variables), all non-secret:

| Variable | Value |
|---|---|
| `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` | sign-in to Azure (OIDC) |
| `TFSTATE_RESOURCE_GROUP`, `TFSTATE_STORAGE_ACCOUNT` | state storage (`TFSTATE_CONTAINER`, default `tfstate`) |
| `TF_DEPLOYER_OBJECT_ID`, `TF_BUDGET_ALERT_EMAIL` | `deployer_object_id`, `budget_alert_email` |
| `TF_SOURCES` (optional), `TF_ALERT_SOURCES` | `sources` (default: the six SEO channels from `variables.tf`), `alert_sources` |
| `TF_TELEGRAM_CHAT_ID`, `TF_TELEGRAM_API_ID`, `TF_TELEGRAM_PHONE` | the matching Terraform variables |
| `TF_COLLECTOR_SCHEDULE`, `TF_DIGEST_SCHEDULE`, `TF_ALERT_SCHEDULE`, `TF_CLEANUP_SCHEDULE` (optional) | CRON in UTC; default: the values in the settings table; `TF_APP_SETTINGS` overrides them |
| `TF_LOCATION`, `TF_NAME_PREFIX` (optional) | default `westeurope` and `signalfeed` |
| `TF_APP_SETTINGS` (optional) | JSON, for example `{"DigestSize":"6"}`; matches `app_settings` |

The secrets (OpenAI, Telegram) are not needed in GitHub: as before, they go into Key Vault with `az keyvault secret set`.
After the first `apply`, copy `function_app_name` from the run summary into `AZURE_FUNCTIONAPP_NAME` for the code deploy.

### Releases and rollback

Every deploy from a push to `main` is a release: a `vYYYY.MM.DD.N` tag and a GitHub Release with generated notes. `YYYY.MM.DD` is the UTC build date and `N` is GitHub's
built-in run counter of `deploy.yml`, which only grows, so versions never collide (numbers have gaps because pull request builds count too). The same version is baked into the build
(`-p:Version`), so it matches the tag. No deploy (see `DEPLOY_ENABLED` below) means no release. Details: [ADR-0005](adr/0005-release-every-deploy-with-a-build-counter-version.md).

**Rollback.** Actions → build-test-deploy → Run workflow, and enter the earlier release tag, for example `v2026.11.20.120`. The workflow rebuilds that tag, tests it and deploys it,
and creates no new release. Or revert the commit through a pull request, which deploys the previous code as a new release. A rollback restores code only: infrastructure,
App Settings, secrets and stored data stay as they are, and a renamed or removed required setting can stop an older version from starting.

The repository must allow Actions to create releases: the release job asks for `contents: write`, so Settings → Actions → General → Workflow permissions must not block it.

**Deploy is off by default.** Without the `DEPLOY_ENABLED` variable set to `true`, the workflow only builds and tests, and the deploy and release steps
are skipped. When you are ready to deploy, set this variable in the same place as the rest.
