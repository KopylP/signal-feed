# infra: Terraform (Azure)

Resource names and variables are described in `README.md` (deployment section). Run it locally (`terraform apply`) or manually through the `infra` workflow.

## Rules

- Terraform never creates secret values (OpenAI, Telegram) and never stores them in state: App Settings only carry Key Vault references.
- Access between resources goes through managed identity and RBAC (`roles.tf`), without keys or connection strings.
- Names: `<abbreviation>-${var.name_prefix}` (`rg-`, `plan-`, `log-`, `appi-`, `budget-`). Globally unique ones (`st`, `kv-`, `func-`) get `local.suffix`.
- A new variable has a `description`, a type, and a `default` when the value is non-secret and the same for everyone. Personal data (phone, email, chat id) gets no `default`.
- Do not add a separate variable for a new agent parameter: its default lives in `AgentOptions.cs` and it is set through `app_settings`.
  Separate variables only for required values, infrastructure values and schedules (timer attributes read them).
- Adding a variable touches several places: `variables.tf`, `terraform.tfvars.example`, `function.tf` (if it is an App Setting), `.github/workflows/infra.yml`
  (`TF_VAR_*` from GitHub Variables) and the variables table in the README. If the variable has a `default`, pass it in the workflow only when it is set, because an empty `TF_VAR_*` overrides the `default`.
- Before finishing: `terraform fmt` and `terraform validate`.
- Do not add resources with a standing cost (Premium plan, VNet, private endpoint, etc.) without an explicit request: the budget is $5 a month.
