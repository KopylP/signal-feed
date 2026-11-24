# Release every deploy with a build-counter version

- Status: Accepted
- Date: 2026-11-24
- Deciders: Maintainer

## Context

The agent is deployed from `main`, and there is one production environment and no consumers of an API, so a version number does not need to
signal compatibility. We want every deployed change to be a traceable release, a version that can never collide, and a cheap way back to an earlier version.
Flex Consumption has no deployment slots, so rollback is a redeploy of older code (see ADR-0002).

## Decision

- A push to `main` that changes code is built, tested, deployed and then released: a tag and a GitHub Release with generated notes.
  Pushes that only touch `docs/**`, `README.md`, `**/CLAUDE.md`, `.claude/**` or `infra/**` do not run the pipeline. `prompts/*.md` do, because they ship with the build.
- The version is `YYYY.MM.DD.N`, tagged `vYYYY.MM.DD.N`, where the date is the UTC build date and `N` is GitHub's built-in `github.run_number`
  of `deploy.yml`. The counter increases on every run and never resets, so versions are unique.
- The version is calculated once in the build job, passed to the build (`-p:Version`) and reused for the tag, so the tag always matches the deployed binary.
- The release job runs only after a successful deploy and only on `push`. No deploy (for example `DEPLOY_ENABLED` is not `true`) means no release.
- Rollback is a manual run of `deploy.yml` with a `tag` input: it rebuilds that tag's code and deploys it, without creating a release.
  `git revert` through a pull request is the other path and produces a new release.

## Options considered

- **SemVer from Conventional Commits**: tells how big a change is, but needs commit discipline and a PR-title check, and the number means little without API consumers.
- **CalVer with a daily counter**: needs a script that reads existing tags, and is easy to get wrong when two runs overlap.
- **A counter stored in a repository variable**: `GITHUB_TOKEN` cannot write repository variables, so it would need a PAT or a GitHub App.
- **Date plus `run_number`**: no state to maintain, no human decision, collision-free. Chosen.
- **Attach the built zip to each release for rollback**: gives bit-identical redeploys but costs storage; a rebuild from the tag with pinned package versions is close enough here.

## Consequences

- `run_number` counts every run of `deploy.yml`, including pull request builds, so release numbers have gaps. That is expected.
- Renaming or recreating `deploy.yml` restarts the counter at 1; the date part still keeps versions unique.
- A rebuilt rollback is not byte-identical, because the .NET SDK is pinned only to `10.0.x`.
- A rollback restores code only. Infrastructure, App Settings, secrets and stored data are not rolled back, and a renamed or removed required setting can stop an older version from starting.
- Merges that skip the pipeline (docs, `infra/**`) are not releases. Infrastructure changes still go through the manual `infra` workflow (ADR-0004).
- The repository needs Actions to be allowed to create releases (`contents: write`), which is granted to the release job only.
