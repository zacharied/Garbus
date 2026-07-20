# Master Release Workflow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish self-contained Windows x64 and Linux x64 ZIPs to one rolling GitHub prerelease after every successful push to `master`.

**Architecture:** One read-only Ubuntu job tests the game, cross-publishes both runtime identifiers, and uploads ready-to-release archives. A second Ubuntu job runs only on `master` pushes, receives repository write permission, and updates the movable `latest-master` tag and prerelease with GitHub CLI.

**Tech Stack:** GitHub Actions, .NET 8 SDK, Bash, Info-ZIP, GitHub CLI

## Global Constraints

- Use only `ubuntu-latest`; do not add a Windows runner.
- Target the repository's integration branch, `master`.
- Do not change project version properties.
- Produce self-contained `win-x64` and `linux-x64` publish outputs.
- Keep pull-request jobs read-only and prevent them from publishing releases.
- Use only the built-in `GITHUB_TOKEN` and official GitHub-maintained actions.
- Keep stable asset names: `Garbus-win-x64.zip`, `Garbus-linux-x64.zip`, and `SHA256SUMS.txt`.
- Preserve `vX.Y.Z` tags for future intentional stable releases.

---

### Task 1: Build And Rolling Release Workflow

**Files:**
- Create: `.github/workflows/release-master.yml`

**Interfaces:**
- Consumes: `Garbus.Desktop/Garbus.Desktop.csproj`, `Garbus.Game.Tests/Garbus.Game.Tests.csproj`, pushes and pull requests targeting `master`
- Produces: validated self-contained ZIP archives, SHA-256 checksums, and the rolling `latest-master` GitHub prerelease

- [ ] **Step 1: Confirm the workflow does not already exist**

Run:

```bash
test ! -e .github/workflows/release-master.yml
```

Expected: exit status 0.

- [ ] **Step 2: Create the workflow**

Create `.github/workflows/release-master.yml` with exactly this structure:

```yaml
name: Master release

on:
  push:
    branches:
      - master
  pull_request:
    branches:
      - master
    paths:
      - .github/workflows/release-master.yml

concurrency:
  group: master-release-${{ github.ref }}
  cancel-in-progress: false

jobs:
  package:
    name: Test and package
    runs-on: ubuntu-latest
    permissions:
      contents: read
    steps:
      - name: Check out source
        uses: actions/checkout@v4

      - name: Set up .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x

      - name: Run tests
        run: dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --configuration Release

      - name: Publish archives
        shell: bash
        run: |
          set -euo pipefail

          rm -rf release
          mkdir -p release

          for rid in win-x64 linux-x64; do
            package_root="release/${rid}"
            publish_dir="${package_root}/Garbus"

            dotnet publish Garbus.Desktop/Garbus.Desktop.csproj \
              --configuration Release \
              --runtime "${rid}" \
              --self-contained true \
              --output "${publish_dir}"

            case "${rid}" in
              win-x64) launch_command='Garbus.exe' ;;
              linux-x64) launch_command='./Garbus' ;;
            esac

            printf '%s\n' \
              'Garbus master build' \
              "Commit: ${GITHUB_SHA}" \
              "Runtime: ${rid}" \
              "Launch: ${launch_command}" \
              > "${publish_dir}/BUILD-INFO.txt"

            (
              cd "${package_root}"
              zip -9 -r "../Garbus-${rid}.zip" Garbus
            )

            rm -rf "${package_root}"
          done

          (
            cd release
            sha256sum Garbus-*.zip > SHA256SUMS.txt
          )

      - name: Upload release files
        uses: actions/upload-artifact@v4
        with:
          name: master-release
          path: |
            release/Garbus-win-x64.zip
            release/Garbus-linux-x64.zip
            release/SHA256SUMS.txt
          if-no-files-found: error
          retention-days: 1
          compression-level: 0

  release:
    name: Update rolling prerelease
    if: github.event_name == 'push' && github.ref == 'refs/heads/master'
    needs: package
    runs-on: ubuntu-latest
    permissions:
      contents: write
    steps:
      - name: Check out tested commit
        uses: actions/checkout@v4
        with:
          ref: ${{ github.sha }}
          fetch-depth: 0

      - name: Download release files
        uses: actions/download-artifact@v4
        with:
          name: master-release
          path: release

      - name: Prepare release metadata
        id: metadata
        shell: bash
        run: |
          set -euo pipefail

          short_sha="${GITHUB_SHA::7}"
          printf 'title=Latest master build (%s)\n' "${short_sha}" >> "${GITHUB_OUTPUT}"
          cat > release-notes.md <<EOF
          Automated playtest build from [\`${GITHUB_SHA}\`](https://github.com/${GITHUB_REPOSITORY}/commit/${GITHUB_SHA}).

          This is a development snapshot from \`master\`, not a stable release.

          - Windows x64: \`Garbus-win-x64.zip\`
          - Linux x64: \`Garbus-linux-x64.zip\`
          - Download verification: \`SHA256SUMS.txt\`
          EOF

      - name: Publish rolling prerelease
        env:
          GH_TOKEN: ${{ github.token }}
          RELEASE_TITLE: ${{ steps.metadata.outputs.title }}
        shell: bash
        run: |
          set -euo pipefail

          tag='latest-master'
          git tag --force "${tag}" "${GITHUB_SHA}"
          git push --force origin "refs/tags/${tag}"

          if gh release view "${tag}" >/dev/null 2>&1; then
            gh release upload "${tag}" release/* --clobber
            gh release edit "${tag}" \
              --title "${RELEASE_TITLE}" \
              --notes-file release-notes.md \
              --prerelease \
              --latest=false
          else
            gh release create "${tag}" release/* \
              --target "${GITHUB_SHA}" \
              --title "${RELEASE_TITLE}" \
              --notes-file release-notes.md \
              --prerelease \
              --latest=false
          fi
```

- [ ] **Step 3: Run static workflow validation**

Run:

```bash
version=1.7.7
archive="/tmp/actionlint_${version}_linux_x86_64.tar.gz"
curl -fsSL \
  "https://github.com/rhysd/actionlint/releases/download/v${version}/actionlint_${version}_linux_x86_64.tar.gz" \
  -o "${archive}"
tar -xzf "${archive}" -C /tmp actionlint
/tmp/actionlint .github/workflows/release-master.yml
```

Expected: exit status 0 with no diagnostics. The validator remains a temporary tool and does not add a
repository dependency.

Run:

```bash
git diff --check
```

Expected: exit status 0 with no output.

- [ ] **Step 4: Inspect workflow permissions and publication guards**

Run:

```bash
grep -nE 'pull_request:|contents: read|contents: write|github.event_name|refs/heads/master|latest-master' .github/workflows/release-master.yml
```

Expected: the package job is read-only, the release job alone is write-enabled, and the release job is
guarded to a push on `master`.

- [ ] **Step 5: Commit the workflow and plan**

```bash
git add .github/workflows/release-master.yml docs/superpowers/plans/2026-07-19-master-release-workflow.md
git commit -m "ci: publish rolling master builds"
```

Expected: one implementation commit is created on `ci/master-release`.

### Task 2: Review, Push, And Draft PR

**Files:**
- Review: `.github/workflows/release-master.yml`
- Review: `docs/superpowers/specs/2026-07-19-master-release-workflow-design.md`
- Review: `docs/superpowers/plans/2026-07-19-master-release-workflow.md`
- Create remotely: Draft pull request against `master`

**Interfaces:**
- Consumes: committed `ci/master-release` branch and static validation evidence
- Produces: reviewed remote branch, hydrated Draft PR, and an Ubuntu Actions packaging run

- [ ] **Step 1: Request an independent code review**

Give the reviewer the design spec, implementation plan, workflow diff, and verification output. Ask for
correctness, GitHub Actions security, release race, archive completeness, and maintainability findings,
ordered by severity with file and line references.

Expected: review findings or an explicit no-findings result.

- [ ] **Step 2: Resolve review findings**

For each valid finding, update the smallest relevant section of
`.github/workflows/release-master.yml`, rerun `actionlint` and `git diff --check`, then commit the fix.

Expected: no unresolved high- or medium-severity findings.

- [ ] **Step 3: Push the feature branch**

Run:

```bash
git push --set-upstream origin ci/master-release
```

Expected: `origin/ci/master-release` points to the reviewed local branch.

- [ ] **Step 4: Open the hydrated Draft PR**

Create a Draft PR with base `master`, head `ci/master-release`, and title
`ci: publish rolling master builds`. Its description must include:

```markdown
## Summary

- test and cross-publish self-contained Windows x64 and Linux x64 builds on Ubuntu
- update one `latest-master` prerelease after successful pushes to `master`
- keep pull-request validation read-only and limit it to release workflow changes

## Distribution

The rolling prerelease exposes `Garbus-win-x64.zip`, `Garbus-linux-x64.zip`, and
`SHA256SUMS.txt`. Each ZIP contains a top-level `Garbus` directory and commit metadata.

## Verification

- `actionlint .github/workflows/release-master.yml`
- `git diff --check`
- independent code review

The local Linux workspace does not have the .NET SDK, so the Draft PR's Ubuntu Actions run is the
first executable test and cross-publish verification. Release publication remains disabled for pull
requests and will first run after merge to `master`.
```

Expected: GitHub reports the PR as Draft and its base branch as `master`.

- [ ] **Step 5: Inspect the PR workflow run**

Use `gh pr checks --watch` for the new PR.

Expected: the `Test and package` job succeeds and `Update rolling prerelease` is skipped. If the job
fails, inspect its logs, fix the workflow, rerun local static checks and review as needed, push the fix,
and wait for a passing run before reporting completion.
