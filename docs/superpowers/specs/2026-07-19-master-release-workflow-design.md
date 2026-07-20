# Master Release Workflow Design

**Date:** 2026-07-19

## Goal

Provide durable GitHub downloads for Garbus playtest builds. Every successful push to
`master` publishes self-contained Windows x64 and Linux x64 ZIP archives without requiring players to
install .NET. Stable semantic versions remain reserved for intentional future releases.

## Release Channel

Each default-branch build creates an immutable GitHub prerelease named `Master build (<short-sha>)` and
tagged `master-<12-char-sha>`. The release body identifies and links the full source commit. Retaining
each successful build lets playtesters download and compare earlier development snapshots.

The workflow does not change project version properties. Future stable releases can use immutable
SemVer tags such as `v0.1.0` without conflicting with the `master-*` development tags.

## Triggers And Concurrency

The workflow runs in two situations:

- A push to `master` builds, tests, packages, and creates a commit-specific prerelease.
- A pull request targeting `master` runs only when the release workflow file changes. It performs the
  same test and packaging work but cannot publish a release.

Master runs are serialized with GitHub Actions concurrency to limit concurrent packaging work. Releases
are commit-specific, so run ordering cannot replace another commit's assets.

## Build And Package

One `ubuntu-latest` job runs the headless test project and cross-publishes
`Garbus.Desktop/Garbus.Desktop.csproj` for `win-x64` and `linux-x64`. Both publishes use the Release
configuration and are self-contained. Garbus does not require Windows-only build tooling: the .NET SDK
can produce the Windows app host and select RID-specific NuGet assets from Linux.

Using one Linux job avoids the Windows runner billing multiplier and avoids restoring dependencies in
two matrix jobs. Native Windows launch testing remains outside this workflow; the repository's existing
development process can perform that separately when needed.

Each archive contains a top-level `Garbus` directory with the executable, all files emitted by
`dotnet publish`, and a generated `BUILD-INFO.txt`. The build information identifies the full commit,
target runtime, and launch command. Repository resources such as charts, tracks, samples, fonts, and
textures are already embedded through `Garbus.Resources` and therefore travel with the publish output.

The public assets have stable names:

- `Garbus-win-x64.zip`
- `Garbus-linux-x64.zip`
- `SHA256SUMS.txt`

The Linux ZIP is created on Linux so its executable permission is retained in the archive metadata.

## Publication And Permissions

The packaging job has read-only repository access and uploads the three release files as one internal
workflow artifact. A separate publication job runs only for pushes to `master`, downloads those files,
and receives `contents: write`. Keeping write permission out of the pull-request build prevents tested
PR code from receiving an unnecessary repository write token.

The publication job uses the GitHub CLI already present on hosted runners and the built-in
`GITHUB_TOKEN`; it requires no custom secrets or third-party release action. Every successful commit
creates its own `master-<12-char-sha>` tag and prerelease. Re-running the workflow updates only that
commit's assets. Development prereleases are not marked as the repository's latest stable release.

## Failure Handling

Tests and both publishes must succeed before publication starts. A failure creates no release for that
commit. The release job uses strict shell error handling, so a tag, release, or asset API failure fails
the workflow visibly. Re-running a failed publication is safe because release editing and stable-name
asset replacement are idempotent within the commit-specific release.

## Verification And Review

Implementation is complete when:

1. The workflow parses successfully and local static checks find no malformed YAML or whitespace
   errors.
2. The Draft PR's Ubuntu run passes tests, produces both ZIPs, and does not create or move a release.
3. Inspection confirms each ZIP has a top-level `Garbus` directory, the expected executable, runtime
   files, and matching `BUILD-INFO.txt`.
4. `SHA256SUMS.txt` verifies both archives.
5. An independent code review finds no unresolved correctness, security, or maintainability issues.
6. After merge, the first successful push workflow creates a `master-<12-char-sha>` prerelease and
   exposes both downloadable archives.

The implementation will be committed and pushed from an isolated worktree, then opened as a Draft PR
against `master`. The PR description will state that publication itself cannot be exercised until the
workflow is present on `master`; the PR build verifies the shared test and packaging path.
