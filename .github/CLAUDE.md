# .github/workflows

- `deploy.yml`: build, tests and code deploy (after a push to `main` when `DEPLOY_ENABLED=true`).
- `infra.yml`: manual `terraform plan` / `apply` (the `apply` checkbox).

## Rules

- Sign in to Azure only through OIDC (`azure/login`), no passwords or keys in GitHub. Settings are passed as GitHub Variables (`vars.*`).
- Pin actions to a major version (`@v4`) and Terraform to an exact version.
- `deploy.yml` never changes infrastructure and `infra.yml` never deploys code. Do not mix them.
- When you add a variable to `infra.yml`, update the GitHub Variables table in the README, and `infra/CLAUDE.md` if the rule changed.
- A new workflow declares the minimum `permissions` it needs and uses `concurrency` where two simultaneous runs are unsafe.
