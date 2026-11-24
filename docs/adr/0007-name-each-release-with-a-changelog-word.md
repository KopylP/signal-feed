# Name each release with a changelog word

- Status: Proposed
- Date: 2026-11-24
- Deciders: Maintainer

## Context

[ADR-0005](0005-release-every-deploy-with-a-build-counter-version.md) tags every deploy `vYYYY.MM.DD.N` and lets GitHub generate the release notes.
We now keep a `CHANGELOG.md` for operators (see the `write-changelog` skill) and want one description of a change, written once, in a pull request.
A pull request cannot know its build number: `N` exists only after the pipeline starts. The changelog section therefore needs a name that is known before the merge
and that the pipeline can later join to the version.

## Decision

- A pull request that needs a changelog entry adds a section headed `## [word] - YYYY-MM-DD`. The word is one random lowercase word (letters only), unique among sections and tags.
- On a push to `main` the build job finds the word the push added (a diff of `CHANGELOG.md`) and builds the tag `vYYYY.MM.DD.N-word`, for example `v2026.11.24.131-falcon`.
  The numeric part is still what is baked into the binary (`-p:Version`).
- After a successful deploy the release job creates the GitHub Release with that tag and the text of the section as its notes, then renames the heading to
  `## [v2026.11.24.131-falcon] - YYYY-MM-DD` and commits that to `main` as `github-actions[bot]`. The word is kept, only the build version is added in front of it.
- Sections still waiting for a version (merged without a pipeline run, for example together with an `infra/**` change) are released with the next deploy:
  their headings get the same build version in front of their own words, and their text joins the release notes. If the push added no section, the oldest waiting word names the tag.
- A push that adds no section and finds none waiting gets a plain `vYYYY.MM.DD.N` tag and generated notes, as before.
- A rollback accepts both tag shapes. `CHANGELOG.md` is in `paths-ignore`, and a push with `GITHUB_TOKEN` starts no workflow, so the changelog commit does not trigger a build.

The logic lives in `.github/scripts/changelog.py` (with tests) and in `deploy.yml`.

## Options considered

- **The word from the changelog, one tag (chosen)**: the tag and the release are created once, after the deploy, from information already in the repository.
- **A tag with the word first, renamed after the build**: closer to the first idea, but git cannot rename a tag, so it means a second tag and deleting the first, and a window where the tag points at an unbuilt commit.
- **Keep `[word]` in the file and only tag**: nothing is committed to `main`, but a reader of `CHANGELOG.md` cannot see which build contains a change.
- **A pending id with a random hex**: works the same way, but a word is easier to say, search and remember.

## Consequences

- The pipeline pushes a commit to `main`, so branch protection must allow `github-actions[bot]` to do so. If the push cannot be made, the release exists but the headings stay as words; the next release renames them.
- Two pull requests that add a section at the top of the file conflict in git. The resolution is to keep both sections.
- Release notes come from the changelog, so a change without a section (an internal-only one) is not described in them unless it is the only thing in a release.
- Tags are longer and the word is not part of the binary's version. The build number is, and it still identifies the build.
- A section merged while a build is running is not renamed by that build: only the words listed when the commit was built are renamed.

## Updates

<!-- Append dated notes only (YYYY-MM-DD: note). Never rewrite the sections above after acceptance. -->
