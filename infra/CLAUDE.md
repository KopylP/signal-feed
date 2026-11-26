# infra: Terraform (Azure)

Resource names and variables are described in `docs/user-guide/installation.md`. Run it locally (`terraform apply`) or manually through the `infra` workflow.

## Rules

- Terraform never creates secret values (OpenAI, Telegram) and never stores them in state: App Settings only carry Key Vault references. The one key it creates, `tg-session`, is generated inside Key Vault (ADR-0017).
- Access between resources goes through managed identity and RBAC (`roles.tf`), without keys or connection strings. The storage account keys are off (`shared_access_key_enabled = false`, `storage_use_azuread = true`): whoever runs Terraform needs the data roles in `roles.tf`.
- The function app's App Settings are written by `azapi_resource_action.app_settings` after every change of the app (`azurerm` injects `AzureWebJobsStorage` with an empty key, #29149): every setting must be in `local.app_settings`, or the PUT removes it. Delete the action when the provider is fixed.
- Names: `<abbreviation>-${var.name_prefix}` (`rg-`, `plan-`, `log-`, `appi-`, `budget-`). Globally unique ones (`st`, `kv-`, `func-`) get `local.suffix`.
- Every resource that accepts `tags` gets `tags = local.tags` (`main.tf`); tags do not inherit from the resource group.
- A new variable has a `description`, a type, and a `default` when the value is non-secret and the same for everyone. Personal data (phone, email, chat id) gets no `default` (the chat id: an empty one) and is `sensitive = true`; the workflow reads it from GitHub Secrets.
- Do not add a separate variable for a new agent parameter: its default lives in `AgentOptions.cs` and it is set through `app_settings`.
  Separate variables only for required values, infrastructure values and schedules (timer attributes read them).
  Schedule precedence: key in `app_settings` > `*_schedule` variable > default. Any other key managed in `function.tf` wins over `app_settings`.
- Adding a variable touches several places: `variables.tf`, `terraform.tfvars.example`, `function.tf` (if it is an App Setting), `.github/workflows/infra.yml`
  (`TF_VAR_*` from GitHub Variables) and the GitHub Variables table in `docs/developer-guide/ci-cd.md`. If the variable has a `default`, pass it in the workflow only when it is set, because an empty `TF_VAR_*` overrides the `default`.
- Before finishing: `terraform fmt` and `terraform validate`.
- Do not add resources with a standing cost (Premium plan, VNet, private endpoint, etc.) without an explicit request: the budget is $5 a month.
