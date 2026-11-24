# .github/workflows

- `deploy.yml`: formatting, then Terraform `validate` (static, no Azure sign-in; both skipped on a manual run). Formatting (on a PR from this repository `dotnet format` and `terraform fmt` commit the fix to the branch as `github-actions[bot]`; on `main`, forks and Dependabot it only verifies), build, tests, code deploy and release (after a push to `main` when `DEPLOY_ENABLED=true`). A manual run with a `tag` redeploys that release and creates no release.
- `infra.yml`: manual `terraform plan` / `apply` (the `apply` checkbox).
- `dependabot.yml`: weekly grouped updates.

## Rules

- Sign in to Azure only through OIDC (`azure/login`), no passwords or keys in GitHub. Settings are passed as GitHub Variables (`vars.*`). Personal data (phone, email, chat id) goes through Secrets (`secrets.*`): the repository is public and Variables are not masked in logs.
- Pin actions to a major version (`@v4`) and Terraform to an exact version (`.terraform-version` at the repo root, read by `deploy.yml` and `infra.yml`).
- The Azure OIDC credential accepts only `refs/heads/main`: `infra` apply and deploy cannot run from a branch, so infra changes merge first and are applied from `main`.
- `deploy.yml` never changes infrastructure and `infra.yml` never deploys code. Do not mix them.
- The version is `YYYY.MM.DD.<run_number>`, calculated once in the build job and reused for the tag, which adds the changelog word: `vYYYY.MM.DD.N-word` (ADR-0005, ADR-0007). `scripts/changelog.py` finds the word and renames the changelog headings; run its tests when you change it. Only the build job (to push the formatting fix on a PR) and the release job get `contents: write`; keep the release job's explicit `if` (it drops the implicit `success()`).
- A commit pushed with `GITHUB_TOKEN` starts no workflow, so the formatting commit gets no check of its own (the check stays on the earlier commit; a re-run does not help). A same-repo PR is built on its branch tip, not on the merge with `main` (ADR-0006).
- Do not list `*.md` in `paths-ignore`: `prompts/*.md` ship with the build. Name documentation files one by one (`README.md`, `CHANGELOG.md`).
- When you add a variable to `infra.yml`, update the GitHub Variables table in `docs/developer-guide/ci-cd.md`, and `infra/CLAUDE.md` if the rule changed.
- A new workflow declares the minimum `permissions` it needs and uses `concurrency` where two simultaneous runs are unsafe.
- A pull request build does not run the paths that exist only on a push (the changelog word, the release job). Reproduce them locally in a full clone before you merge, and never write `cond && 0 || x` in an expression (0 is falsy).
