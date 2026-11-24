# Fix formatting and validate Terraform in the build job

- Status: Accepted
- Date: 2026-11-24
- Deciders: Maintainer

## Context

Formatting and Terraform syntax checks were either missing or lived in separate workflows (`terraform-check.yml`, and a short-lived `format.yml`).
Each extra workflow repeated the checkout and tool setup, added one more entry to the Actions list, and could pass or fail independently of the build that gates the deploy.
The maintainer wants one pipeline, `deploy.yml`, where the checks are part of the build and a broken change cannot reach the deploy.
Related: [ADR-0004](0004-run-terraform-from-a-manual-workflow.md) keeps infrastructure changes in the manual `infra` workflow; this decision only adds static checks.

## Decision

The `build-test` job of `.github/workflows/deploy.yml` runs `dotnet format` and `terraform fmt`, then `terraform validate` (no backend, no Azure sign-in), before the build.
On a pull request from this repository the formatters fix the files and `github-actions[bot]` commits the result to the PR branch, and the build continues on the fixed code.
On `main`, on forks and on Dependabot pull requests (read-only token) the same step only verifies and fails on a difference. A manual run (rollback of an older tag) skips these steps.
The Terraform version lives in `.terraform-version`, read by `deploy.yml` and `infra.yml`. Style rules live in `.editorconfig`.

## Options considered

- **Separate workflows per check**: independent and easy to read, but duplicated setup, more runs, and the deploy does not depend on them.
- **Verify only, never fix**: no extra write permission, but every formatting slip costs a manual round trip.
- **Fix and commit in the build job (chosen)**: one workflow and no round trips; the cost is `contents: write` on the build job and a commit the CI does not test.

## Consequences

- The build job has `contents: write`; the push step runs only for same-repository, non-Dependabot pull requests.
- A commit pushed with `GITHUB_TOKEN` starts no workflow, so the formatting commit has no check of its own; the check stays on the earlier commit. If branch protection ever requires a check on the PR head, push another commit or use a GitHub App token.
- A same-repository PR is built on its branch tip, not on the merge with `main`, so that the fix can be pushed to the branch.
- A push to `main` that only changes `infra/` does not run `deploy.yml` (`paths-ignore`), so Terraform is checked on pull requests only.
- Revisit if the repository gains branch protection with required checks, or if more checks join the build job.

## Updates

<!-- Append dated notes only (YYYY-MM-DD: note). Never rewrite the sections above after acceptance. -->
