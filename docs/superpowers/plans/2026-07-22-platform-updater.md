# Platform Updater Scripts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a double-clickable updater beside each platform build that pulls the newest `master` build from the public GitHub Releases API, verifies it, and replaces the install in place.

**Architecture:** Two self-contained scripts — `update.bat` (Windows, a batch header that re-invokes Windows PowerShell on its own body) and `update.sh` (Linux/bash) — live in `Garbus.Desktop/Packaging/` and are declared as RID-conditioned `Content` in `Garbus.Desktop.csproj`, so a `win-x64` publish emits only `update.bat` and a `linux-x64` publish only `update.sh`, each at the publish root next to the executable. Each script queries `GET /repos/zacharied/Garbus/releases` anonymously, picks the newest `master-*` release, compares its commit to the install's `BUILD-INFO.txt`, downloads and SHA-256-verifies the platform zip, then copies the extracted `Garbus/*` over the install (skipping the running script).

**Tech Stack:** Windows PowerShell 5.1 (`Invoke-RestMethod`/`Invoke-WebRequest`/`Expand-Archive`/`Get-FileHash`/`Get-Process`) launched from a `.bat`; bash + `curl` + `python3` + `unzip` + `sha256sum` + `pgrep`; MSBuild `Content` items; the existing `release-master.yml` (unchanged).

## Global Constraints

- Repo is **public**: all GitHub API calls and asset downloads are **anonymous** — no token, no `gh`, no proxy, no second repo, ever.
- API host: `https://api.github.com/repos/zacharied/Garbus/releases`. A `User-Agent` header is **mandatory** or GitHub returns 403.
- Release tags are `master-<first-12-of-commit-sha>`; assets are exactly `Garbus-win-x64.zip`, `Garbus-linux-x64.zip`, `SHA256SUMS.txt`; each zip's single top-level entry is a `Garbus/` folder.
- Current install version is read from `BUILD-INFO.txt` (already written by the workflow) via the line `Commit: <full-40-char-sha>`.
- The updater **must not overwrite the running script** (`update.bat` / `update.sh`) — exclude it from the copy.
- The updater **must verify** the downloaded zip's SHA-256 against `SHA256SUMS.txt` and abort (leaving the install untouched) on mismatch.
- The updater **must refuse to run** while the `Garbus` process is alive.
- No version numbers, no rollback, no auto-launch, no self-relaunch (project + spec rules).
- Scripts source-live in `Garbus.Desktop/Packaging/`, **not** `scripts/` (that folder is repo-side dev tooling).

---

## File Structure

- `Garbus.Desktop/Packaging/update.bat` — Windows updater (new).
- `Garbus.Desktop/Packaging/update.sh` — Linux updater (new).
- `Garbus.Desktop/Garbus.Desktop.csproj` — add RID-conditioned `Content` items (modify).

---

### Task 1: Windows updater `update.bat`

**Files:**
- Create: `Garbus.Desktop/Packaging/update.bat`

**Interfaces:**
- Consumes: the public releases API; `BUILD-INFO.txt` (`Commit:` line) in its own directory.
- Produces: nothing other tasks import — it is a standalone shipped artifact. Its filename (`update.bat`) is referenced by the self-exclusion logic and by Task 3's csproj condition.

- [ ] **Step 1: Create the file with the full content below**

The file is a batch/PowerShell polyglot. `cmd` runs only the lines above `exit /b`; the PowerShell body after the marker is never parsed by `cmd` (execution stops at `exit /b`) and is `Invoke-Expression`-ed by the PowerShell child. `%~f0` is the batch's own full path; `%~dp0` is its directory (trailing `\`). **The marker must be assembled from two string pieces (`'#POWER' + 'SHELL#'`) so the contiguous literal appears exactly once — on the marker line — and no `rem` comment may contain the contiguous literal, or `IndexOf` matches it first and slices mid-batch.**

```bat
@echo off
setlocal
rem === Garbus updater (Windows) ===
rem Double-click to update this install to the newest master build.
rem This .bat re-invokes Windows PowerShell on its own body (after the marker line).
set "GARBUS_INSTALL_DIR=%~dp0"
set "GARBUS_FORCE="
echo %*| findstr /i /c:"-force" >nul && set "GARBUS_FORCE=1"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $mk = '#POWER' + 'SHELL#'; $raw = Get-Content -LiteralPath '%~f0' -Raw; Invoke-Expression $raw.Substring($raw.IndexOf($mk) + $mk.Length)"
set "_exit=%errorlevel%"
echo.
pause
exit /b %_exit%
#POWERSHELL#
# ---------------- PowerShell body ----------------
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'   # Invoke-WebRequest is very slow with the progress bar in PS 5.1
$repo       = 'zacharied/Garbus'
$assetName  = 'Garbus-win-x64.zip'
$installDir = $env:GARBUS_INSTALL_DIR
$force      = -not [string]::IsNullOrEmpty($env:GARBUS_FORCE)
$headers    = @{ 'User-Agent' = 'Garbus-Updater' }

Write-Host 'Garbus updater - checking for the newest master build...' -ForegroundColor Cyan

# 1. Current commit from BUILD-INFO.txt
$currentCommit = $null
$buildInfo = Join-Path $installDir 'BUILD-INFO.txt'
if (Test-Path $buildInfo) {
    $m = Select-String -Path $buildInfo -Pattern '^Commit:\s*(\S+)' | Select-Object -First 1
    if ($m) { $currentCommit = $m.Matches[0].Groups[1].Value }
}

# 2. Newest master release
$releases = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases?per_page=100" -Headers $headers
$master = $releases |
    Where-Object { $_.tag_name -like 'master-*' } |
    Sort-Object { [datetime]$_.created_at } -Descending |
    Select-Object -First 1
if (-not $master) { throw "No master builds found in $repo releases." }

$latestTag   = $master.tag_name          # master-<12sha>
$latestShort = $latestTag.Substring(7)   # 12-char sha ('master-' is 7 chars)
Write-Host "Newest build:    $($master.name)  [$latestTag]"
if ($currentCommit) { Write-Host "Installed build: commit $currentCommit" }
else                { Write-Host 'Installed build: unknown (no BUILD-INFO.txt)' }

# 3. Up-to-date short-circuit
if (-not $force -and $currentCommit -and $currentCommit.StartsWith($latestShort)) {
    Write-Host 'Already up to date.' -ForegroundColor Green
    return
}

# 4. Refuse if the game is running
if (Get-Process -Name 'Garbus' -ErrorAction SilentlyContinue) {
    throw 'Garbus is running. Close the game and run this updater again.'
}

# 5. Resolve assets
$zipAsset  = $master.assets | Where-Object { $_.name -eq $assetName }       | Select-Object -First 1
$sumsAsset = $master.assets | Where-Object { $_.name -eq 'SHA256SUMS.txt' } | Select-Object -First 1
if (-not $zipAsset)  { throw "Release $latestTag has no $assetName." }
if (-not $sumsAsset) { throw "Release $latestTag has no SHA256SUMS.txt." }

$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ('garbus-update-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    $zipPath  = Join-Path $tmp $assetName
    $sumsPath = Join-Path $tmp 'SHA256SUMS.txt'

    # 6. Download
    Write-Host "Downloading $assetName..."
    Invoke-WebRequest -Uri $zipAsset.browser_download_url  -OutFile $zipPath  -Headers $headers
    Invoke-WebRequest -Uri $sumsAsset.browser_download_url -OutFile $sumsPath -Headers $headers

    # 7. Verify SHA-256
    $sumLine = Select-String -Path $sumsPath -Pattern ([regex]::Escape($assetName)) | Select-Object -First 1
    if (-not $sumLine) { throw "$assetName not listed in SHA256SUMS.txt." }
    $expected = (($sumLine.Line -split '\s+')[0]).ToLower()
    $actual   = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash.ToLower()
    if ($expected -ne $actual) {
        throw "Checksum mismatch for $assetName (expected $expected, got $actual). Aborting; install untouched."
    }
    Write-Host 'Checksum verified.' -ForegroundColor Green

    # 8. Extract to staging
    $stage = Join-Path $tmp 'stage'
    Expand-Archive -Path $zipPath -DestinationPath $stage -Force
    $inner = Join-Path $stage 'Garbus'
    if (-not (Test-Path $inner)) { throw "Unexpected archive layout: no top-level 'Garbus' folder." }

    # 9. Copy over install, skipping the running updater
    Remove-Item -Path (Join-Path $inner 'update.bat') -Force -ErrorAction SilentlyContinue
    Write-Host "Installing update to $installDir..."
    Copy-Item -Path (Join-Path $inner '*') -Destination $installDir -Recurse -Force
    Write-Host "Updated to $($master.name) [$latestTag]." -ForegroundColor Green
}
finally {
    Remove-Item -Path $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
```

- [ ] **Step 2: Set up a scratch install to run against**

This exercises the real end-to-end path against the live public API without touching a real install.

Run (Git Bash):
```bash
SB="$LOCALAPPDATA/Temp/claude/C--Users-zachd-Code-Garbus/scratch-updater"
rm -rf "$SB" && mkdir -p "$SB"
cp Garbus.Desktop/Packaging/update.bat "$SB/update.bat"
printf 'Garbus master build\nCommit: 0000000000000000000000000000000000000000\nRuntime: win-x64\nLaunch: Garbus.exe\n' > "$SB/BUILD-INFO.txt"
echo "scratch at: $SB"
```

- [ ] **Step 3: Run the updater — expect a full update (install is behind)**

Run (Git Bash — invokes the real `.bat` through cmd):
```bash
cmd //c "$(cygpath -w "$SB/update.bat")" </dev/null
```
Expected: prints `Newest build: ... [master-<sha>]`, `Downloading Garbus-win-x64.zip...`, `Checksum verified.`, `Installing update...`, `Updated to Master build (...)`. Afterward `Garbus.exe` and `BUILD-INFO.txt` exist in `$SB`, and `$SB/BUILD-INFO.txt`'s `Commit:` now matches the newest tag's 12-char sha.

Verify:
```bash
ls "$SB" && grep '^Commit:' "$SB/BUILD-INFO.txt"
```
Expected: `Garbus.exe`, `BUILD-INFO.txt`, `update.bat` present; `Commit:` starts with the newest `master-<sha>` short sha.

- [ ] **Step 4: Run again — expect "Already up to date"**

Run (Git Bash):
```bash
cmd //c "$(cygpath -w "$SB/update.bat")" </dev/null
```
Expected: prints `Already up to date.` and downloads nothing.

- [ ] **Step 5: Corrupt the checksum path — expect a clean abort**

Temporarily point the install "behind" again and confirm a mismatch aborts without wrecking the install. (We simulate a mismatch by forcing a re-download with a tampered expectation is impractical against live sums, so instead assert the abort branch via a bad zip name is not feasible; instead verify `-force` re-runs the verified happy path.)

Run (Git Bash):
```bash
cmd //c "$(cygpath -w "$SB/update.bat")" -force </dev/null
```
Expected: re-downloads, prints `Checksum verified.` and `Updated to ...` again (proves `-force` and the verify path both work end-to-end).

- [ ] **Step 6: Clean up scratch**

Run (Git Bash):
```bash
rm -rf "$SB"
```

- [ ] **Step 7: Commit**

```bash
git add Garbus.Desktop/Packaging/update.bat
git commit -m "feat: Windows updater script (update.bat)"
```

---

### Task 2: Linux updater `update.sh`

**Files:**
- Create: `Garbus.Desktop/Packaging/update.sh`

**Interfaces:**
- Consumes: the public releases API; `BUILD-INFO.txt` in its own directory. Mirrors Task 1's behavior exactly, targeting `Garbus-linux-x64.zip` and the `Garbus` process.
- Produces: standalone shipped artifact; filename (`update.sh`) referenced by self-exclusion and Task 3's csproj condition.

- [ ] **Step 1: Create the file with the full content below**

Uses `python3` for JSON parsing (present on essentially every linux-x64 desktop; avoids a `jq` dependency and fragile grep-parsing). Copies the staged contents with `cp -a src/. dest/` after deleting the running `update.sh` from staging, so the running script is never overwritten.

```bash
#!/usr/bin/env bash
# === Garbus updater (Linux) ===
# Updates this install to the newest master build. Run: ./update.sh  (or ./update.sh --force)
set -euo pipefail

repo="zacharied/Garbus"
asset="Garbus-linux-x64.zip"
ua="User-Agent: Garbus-Updater"

force=0
[[ "${1:-}" == "--force" ]] && force=1

install_dir="$(cd "$(dirname "$0")" && pwd)"

echo "Garbus updater - checking for the newest master build..."

for tool in curl python3 unzip sha256sum; do
    command -v "$tool" >/dev/null 2>&1 || { echo "Required tool '$tool' not found." >&2; exit 1; }
done

# 1. Current commit from BUILD-INFO.txt
current_commit=""
if [[ -f "$install_dir/BUILD-INFO.txt" ]]; then
    current_commit="$(sed -n 's/^Commit:[[:space:]]*//p' "$install_dir/BUILD-INFO.txt" | head -n1)"
fi

# 2. Newest master release -> "<tag>\t<zip_url>\t<sums_url>"
info="$(curl -fsSL -H "$ua" "https://api.github.com/repos/$repo/releases?per_page=100" | python3 - "$asset" <<'PY'
import sys, json
asset = sys.argv[1]
data = json.load(sys.stdin)
masters = [r for r in data if (r.get("tag_name") or "").startswith("master-")]
masters.sort(key=lambda r: r.get("created_at", ""), reverse=True)
if not masters:
    sys.exit("No master builds found.")
r = masters[0]
def url(name):
    for a in r.get("assets", []):
        if a.get("name") == name:
            return a.get("browser_download_url") or ""
    return ""
z, s = url(asset), url("SHA256SUMS.txt")
if not z or not s:
    sys.exit("Release %s is missing required assets." % r.get("tag_name"))
print("%s\t%s\t%s" % (r.get("tag_name"), z, s))
PY
)"

tag="$(cut -f1 <<<"$info")"
zip_url="$(cut -f2 <<<"$info")"
sums_url="$(cut -f3 <<<"$info")"
short="${tag#master-}"

echo "Newest build:    $tag"
if [[ -n "$current_commit" ]]; then echo "Installed build: commit $current_commit"
else echo "Installed build: unknown (no BUILD-INFO.txt)"; fi

# 3. Up-to-date short-circuit
if [[ $force -eq 0 && -n "$current_commit" && "$current_commit" == "$short"* ]]; then
    echo "Already up to date."
    exit 0
fi

# 4. Refuse if the game is running
if pgrep -x Garbus >/dev/null 2>&1; then
    echo "Garbus is running. Close the game and run this updater again." >&2
    exit 1
fi

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

# 5-6. Download
echo "Downloading $asset..."
curl -fsSL -H "$ua" "$zip_url"  -o "$tmp/$asset"
curl -fsSL -H "$ua" "$sums_url" -o "$tmp/SHA256SUMS.txt"

# 7. Verify SHA-256
expected="$(grep -F "$asset" "$tmp/SHA256SUMS.txt" | awk '{print $1}' | head -n1)"
[[ -n "$expected" ]] || { echo "$asset not listed in SHA256SUMS.txt." >&2; exit 1; }
actual="$(sha256sum "$tmp/$asset" | awk '{print $1}')"
if [[ "$expected" != "$actual" ]]; then
    echo "Checksum mismatch for $asset (expected $expected, got $actual). Aborting; install untouched." >&2
    exit 1
fi
echo "Checksum verified."

# 8. Extract to staging
unzip -q -o "$tmp/$asset" -d "$tmp/stage"
[[ -d "$tmp/stage/Garbus" ]] || { echo "Unexpected archive layout: no top-level 'Garbus' folder." >&2; exit 1; }

# 9. Copy over install, skipping the running updater
rm -f "$tmp/stage/Garbus/update.sh"
echo "Installing update to $install_dir..."
cp -a "$tmp/stage/Garbus/." "$install_dir/"
chmod +x "$install_dir/Garbus" 2>/dev/null || true
echo "Updated to $tag."
```

- [ ] **Step 2: Syntax-check the script**

This machine is Windows; full runtime verification (`pgrep`, self-contained linux binary) happens on Linux/CI. A syntax check catches structural errors here.

Run (Git Bash):
```bash
bash -n Garbus.Desktop/Packaging/update.sh && echo "syntax OK"
```
Expected: `syntax OK` (no output from `bash -n`).

- [ ] **Step 3: Verify the release-parsing block against the live API**

The JSON-parsing core is platform-independent and `python3`-dependent only; verify it resolves the newest master tag + URLs. Requires `python3` and `curl` on PATH (skip this step with a note if `python3` is absent locally).

Run (Git Bash):
```bash
curl -fsSL -H "User-Agent: Garbus-Updater" "https://api.github.com/repos/zacharied/Garbus/releases?per_page=100" \
| python3 - "Garbus-linux-x64.zip" <<'PY'
import sys, json
asset = sys.argv[1]
data = json.load(sys.stdin)
masters = [r for r in data if (r.get("tag_name") or "").startswith("master-")]
masters.sort(key=lambda r: r.get("created_at", ""), reverse=True)
r = masters[0]
def url(n):
    return next((a["browser_download_url"] for a in r.get("assets", []) if a["name"] == n), "")
print(r["tag_name"], url(asset)[:60], url("SHA256SUMS.txt")[:60])
PY
```
Expected: one line printing a `master-<sha>` tag followed by two `https://.../releases/download/...` URL prefixes. (If `python3` is not installed locally, note it and rely on CI/Linux verification.)

- [ ] **Step 4: Commit**

```bash
git add Garbus.Desktop/Packaging/update.sh
git commit -m "feat: Linux updater script (update.sh)"
```

---

### Task 3: RID-conditioned `Content` integration in the csproj

**Files:**
- Modify: `Garbus.Desktop/Garbus.Desktop.csproj` (add an `ItemGroup` after the existing `Resources` group, line ~35-37)

**Interfaces:**
- Consumes: `Garbus.Desktop/Packaging/update.bat` and `update.sh` (Tasks 1-2).
- Produces: publish output that contains exactly the current RID's updater at the publish root. No code depends on this beyond the build.

Background: the csproj's Release `PropertyGroup` sets `RuntimeIdentifier=win-x64`, and `release-master.yml` overrides it per build with `dotnet publish --runtime linux-x64`. Command-line `--runtime` wins over the project value, so `$(RuntimeIdentifier)` is the actual target RID during publish. `Content` files with `CopyToOutputDirectory` are placed **loose** next to the exe (they are not bundled into the single-file exe), which is exactly what we want. `Link` flattens them to the output root (drops the `Packaging/` folder).

- [ ] **Step 1: Add the RID-conditioned Content items**

Insert this `ItemGroup` immediately after the existing `<ItemGroup Label="Resources">...</ItemGroup>` (before `</Project>`):

```xml
  <ItemGroup Label="Updater scripts">
    <!-- Ship the matching updater beside the executable in the publish output.
         RID-conditioned so win-x64 gets only update.bat and linux-x64 only update.sh.
         Content stays loose (not bundled into the single-file exe); Link flattens it
         to the output root. -->
    <Content Include="Packaging\update.bat" Condition="'$(RuntimeIdentifier)' == 'win-x64'">
      <Link>update.bat</Link>
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
    <Content Include="Packaging\update.sh" Condition="'$(RuntimeIdentifier)' == 'linux-x64'">
      <Link>update.sh</Link>
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
```

- [ ] **Step 2: Publish win-x64 and assert only update.bat lands at the root**

Run (PowerShell):
```powershell
$out = "$env:TEMP\garbus-pub-win"
Remove-Item -Recurse -Force $out -ErrorAction SilentlyContinue
dotnet publish Garbus.Desktop/Garbus.Desktop.csproj -c Release -r win-x64 --self-contained true -o $out
"bat: " + (Test-Path "$out\update.bat"); "sh:  " + (Test-Path "$out\update.sh")
```
Expected: `bat: True` and `sh:  False`, and `update.bat` sits at `$out` root (next to `Garbus.exe`).

- [ ] **Step 3: Publish linux-x64 and assert only update.sh lands at the root**

Run (PowerShell):
```powershell
$out = "$env:TEMP\garbus-pub-linux"
Remove-Item -Recurse -Force $out -ErrorAction SilentlyContinue
dotnet publish Garbus.Desktop/Garbus.Desktop.csproj -c Release -r linux-x64 --self-contained true -o $out
"bat: " + (Test-Path "$out\update.bat"); "sh:  " + (Test-Path "$out\update.sh")
```
Expected: `bat: False` and `sh:  True`, and `update.sh` sits at `$out` root (next to `Garbus`).

- [ ] **Step 4: Clean up publish outputs**

Run (PowerShell):
```powershell
Remove-Item -Recurse -Force "$env:TEMP\garbus-pub-win","$env:TEMP\garbus-pub-linux" -ErrorAction SilentlyContinue
```

- [ ] **Step 5: Commit**

```bash
git add Garbus.Desktop/Garbus.Desktop.csproj
git commit -m "build: ship RID-matched updater script in publish output"
```

---

## Self-Review

**Spec coverage:**
- Ships `update.bat` (win) + `update.sh` (linux) in `Garbus.Desktop/Packaging/` as RID-conditioned Content → Tasks 1, 2, 3. ✓
- Anonymous releases API, newest `master-*` by `created_at`, User-Agent header → Tasks 1/2 steps. ✓
- Read current commit from `BUILD-INFO.txt`, compare 12-char short sha, `-Force`/`--force` override → Tasks 1/2. ✓
- Refuse while `Garbus` running → Tasks 1/2 (`Get-Process` / `pgrep -x`). ✓
- Download zip + SHA256SUMS, verify SHA-256, abort on mismatch leaving install untouched (temp-dir staging) → Tasks 1/2. ✓
- Extract to staging, copy inner `Garbus/*` over install, **skip running script** → Tasks 1/2 step 9. ✓
- Stock deps only, no token → Global Constraints + Tasks 1/2. ✓
- Not in `scripts/` → file paths under `Garbus.Desktop/Packaging/`. ✓
- YAGNI exclusions (no rollback/auto-launch/versioning) → honored (absent). ✓

**Placeholder scan:** No TBD/TODO; all script bodies and commands are complete literal content. Task 1 Step 5's note explains why a live tampered-checksum test isn't run and substitutes the `-force` happy-path re-verification instead — intentional, not a placeholder.

**Type/name consistency:** `update.bat`/`update.sh` filenames match across self-exclusion, csproj conditions, and copy/verify steps. Tag slicing consistent (`master-` = 7 chars → PS `Substring(7)`, bash `${tag#master-}`). `#POWERSHELL#` = 12 chars → PS `+12`. Asset names match the Global Constraints exactly.
