# .github/workflows

- `deploy.yml`: Terraform `fmt -check` and `validate` (static, no Azure sign-in; every PR and push to `main`, not a manual run), formatting (on a PR from this repository `dotnet format` commits the fix to the branch as `github-actions[bot]`; on `main`, forks and Dependabot it only verifies), build, tests, code deploy and release (after a push to `main` when `DEPLOY_ENABLED=true`). A manual run with a `tag` redeploys that release and creates no release.
- `infra.yml`: manual `terraform plan` / `apply` (the `apply` checkbox).
- `dependabot.yml`: weekly grouped updates.

## Rules

- Sign in to Azure only through OIDC (`azure/login`), no passwords or keys in GitHub. Settings are passed as GitHub Variables (`vars.*`).
- Pin actions to a major version (`@v4`) and Terraform to an exact version (`TERRAFORM_VERSION` in `deploy.yml`; keep `infra.yml` on the same one).
- The Azure OIDC credential accepts only `refs/heads/main`: `infra` apply and deploy cannot run from a branch, so infra changes merge first and are applied from `main`.
- `deploy.yml` never changes infrastructure and `infra.yml` never deploys code. Do not mix them.
- The version is `YYYY.MM.DD.<run_number>`, calculated once in the build job and reused for the tag (ADR-0005). Only the build job (to push the formatting fix on a PR) and the release job get `contents: write`; keep the release job's explicit `if` (it drops the implicit `success()`).
- A commit pushed with `GITHUB_TOKEN` does not start workflows, so the `Apply dotnet format` commit has no CI run of its own; push again or re-run if you need one.
- Do not list `*.md` in `paths-ignore`: `prompts/*.md` ship with the build.
- When you add a variable to `infra.yml`, update the GitHub Variables table in the README, and `infra/CLAUDE.md` if the rule changed.
- A new workflow declares the minimum `permissions` it needs and uses `concurrency` where two simultaneous runs are unsafe.
