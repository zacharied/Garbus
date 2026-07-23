# Platform updater scripts — design

## Purpose

Ship a self-contained updater alongside each platform build so a playtester can pull the
newest `master` build over their existing install without touching git, `dotnet`, or a
GitHub account. It sits next to the executable in the extracted install folder; the tester
runs it, it fetches the latest automated master build from GitHub Releases, verifies it, and
replaces the install in place.

`zacharied/Garbus` is a **public** repo, so the GitHub Releases API and asset downloads are
reachable anonymously — the updater ships **no token, needs no proxy, and talks to no second
repo**.

## What ships, and where the source lives

Two variants, one per published runtime:

- **Windows (`win-x64`): `update.bat`** — a single, double-clickable file. `.ps1` can't be
  launched by double-click (Windows opens it in an editor), so the entry point is batch. The
  batch header re-invokes Windows PowerShell on the script's own body:
  - `powershell -NoProfile -ExecutionPolicy Bypass` (runs regardless of the machine's
    execution policy), reading everything after a `#POWERSHELL#` marker line and
    `Invoke-Expression`-ing it.
  - Ends with `pause` so the console window stays open for the tester to read the result.
- **Linux (`linux-x64`): `update.sh`** — a POSIX/bash script.

**Repo home:** `Garbus.Desktop/Packaging/update.bat` and `Garbus.Desktop/Packaging/update.sh`,
declared in `Garbus.Desktop.csproj` as `Content` with `CopyToOutputDirectory=PreserveNewest`,
each conditioned on the `RuntimeIdentifier` so a `win-x64` publish gets only `update.bat` and a
`linux-x64` publish gets only `update.sh`. They copy to the **root of the publish output** —
next to `Garbus.exe` / `Garbus` — for both local `dotnet publish` and `release-master.yml`. No
workflow change is required.

They are **not** in `scripts/`: that folder is repo-side developer tooling
(`ImportAllowedCharts.ps1` touches the local Google Drive and `Garbus.Resources`). The updater
is a shipped end-user artifact and has no meaning inside a repo checkout.

## Flow (identical logic across both platforms)

1. **Locate install dir** = the directory the script is running from (it lives inside the
   extracted `Garbus/` folder, beside the executable).
2. **Read current commit** from the existing `BUILD-INFO.txt` in the install dir — the release
   workflow already writes `Commit: <full-sha>` into it. No new version-marker file is
   introduced.
3. **Find the newest master build:** `GET https://api.github.com/repos/zacharied/Garbus/releases`
   (anonymous), keep releases whose `tag_name` starts with `master-`, pick the one with the
   most recent `created_at`.
4. **Compare:** the release tag is `master-<first-12-of-sha>`. If it matches the current
   commit's first 12 chars, print "already up to date" and exit — **unless** `-Force` (Windows)
   / `--force` (Linux) is passed.
5. **Refuse if the app is running:** if a `Garbus.exe` (Windows) / `Garbus` (Linux) process is
   present, print a clear message and exit without touching files (the copy would fail on locked
   files anyway).
6. **Download + verify:** download the platform asset (`Garbus-win-x64.zip` /
   `Garbus-linux-x64.zip`) and `SHA256SUMS.txt` (via each asset's `browser_download_url`) into a
   temp directory. Compute the zip's SHA-256 and compare against its line in `SHA256SUMS.txt`.
   Abort on mismatch, leaving the install untouched.
7. **Staged swap:** extract the verified zip into a temp staging directory (the zip's top-level
   entry is `Garbus/`), then copy the inner `Garbus/*` over the install dir, overwriting — **but
   skip the running updater script itself** (`update.bat` / `update.sh`; see below).
   Extract-then-copy (never extract in place) means a failed or corrupt download can't leave a
   half-updated install.
8. **Report:** print the new commit / release title and exit. On Windows the trailing `pause`
   holds the window open.

## Dependencies (all stock — nothing to install)

- **Windows:** `powershell.exe` (Windows PowerShell 5.1, present on every supported Windows),
  using `Invoke-RestMethod`, `Invoke-WebRequest`, `Expand-Archive`, `Get-FileHash`,
  `Get-Process`. No `gh`, no modules, no token.
- **Linux:** `curl`, `unzip`, `sha256sum`, `pgrep` — standard on any `linux-x64` desktop target.

**The updater skips overwriting itself.** It overwrites the folder it lives in, but the copy
step excludes the currently-running script (`update.bat` on Windows, `update.sh` on Linux).
`cmd.exe` tracks its position in a running `.bat` by byte offset and holds a file handle, so
overwriting it mid-run risks a sharing violation (a mid-copy failure) or corrupt continuation;
a running bash script has the same hazard. Consequence: the updater logic itself is **not**
self-updated — if it changes in a future build, testers re-extract the release zip manually to
pick up the new script. Acceptable for a playtest tool.

## Assumptions / limitations (acceptable for a playtest tool)

- The updater assumes it is run from inside the extracted `Garbus/` install folder; run from a
  stray copy elsewhere, it would overwrite that location. Fine for the intended use.
- Unauthenticated GitHub API is rate-limited to 60 requests/hour per IP — far above occasional
  update checks.
- Verification trusts `SHA256SUMS.txt` from the same release (integrity against a corrupted
  download, not a supply-chain signature). No GPG signing — out of scope.

## Explicitly not doing (YAGNI)

- No auto-launch of the game after updating.
- No rollback / backup of the previous install.
- No scheduling or background update checks.
- No version numbers (project rule: no versioning).
- No self-relaunch or elevation.

## Testing

These are shipped shell/batch scripts, not part of the .NET test suite, so they are validated
by:

- **Build check:** a `win-x64` and a `linux-x64` `dotnet publish` each place exactly their own
  updater variant at the publish root (and not the other's).
- **Manual smoke:** run `update.bat` against an install that is behind master (updates), current
  (no-ops), and with `-Force` (re-downloads); confirm SHA mismatch aborts cleanly and a running
  app is refused.
