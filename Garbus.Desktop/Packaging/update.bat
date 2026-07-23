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
