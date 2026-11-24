# Keep secret values out of Terraform state

- Status: Accepted
- Date: 2026-11-24
- Deciders: Maintainer

## Context

The agent needs three secrets: `OpenAIApiKey`, `TelegramBotToken`, `TelegramApiHash`. Terraform state is stored in plain text, and in CI it is read by a pipeline identity.
Recorded retroactively; see `infra/keyvault.tf`, `infra/function.tf` and `infra/roles.tf`.

## Decision

Terraform creates the Key Vault (RBAC mode) but not the secret values. The values are set by hand with `az keyvault secret set`,
and the function reads them through Key Vault references in App Settings, using its managed identity (role `Key Vault Secrets User`).
Access to storage also uses the managed identity, with no keys or connection strings.

## Options considered

- **Secret values as Terraform variables**: simple, but the values end up in state and in the CI configuration.
- **Secrets as GitHub secrets, applied by the workflow**: the pipeline would handle the plain values.
- **Key Vault references with manually set values**: one extra manual step, but the values never touch Terraform or GitHub.

## Consequences

Setting up an environment needs a manual step per secret (documented in the README). An unresolved reference shows up as a startup error naming the secret.
Rotating a secret is done in Key Vault and may need `az functionapp restart`. The deployer needs the `Key Vault Secrets Officer` role to set values.

## Updates

2026-11-24: The setup steps are no longer in the README. They moved to [installation](../user-guide/installation.md) (secrets in Key Vault); the README is now a short entry point.
