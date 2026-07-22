<#
Imports charts approved in G:\My Drive\Garbus\Charts\AllowedCharts.txt into this repo's Garbus.Resources.

Each line in AllowedCharts.txt is the name of a folder under G:\My Drive\Garbus\Charts containing a
song (one .garbus file + one audio track + optionally one jacket image, e.g. "aerodynamic"). A song
is now a single multi-chart .garbus file, so it needs no folder in the repo. For each listed folder,
this script:
  1. Copies the .garbus file into Garbus.Resources\Charts, renamed to "<folder name>.garbus" (it sits
     flat in the Charts resource namespace - no subfolder).
  2. Copies the audio track into Garbus.Resources\Tracks, renamed to "<folder name>.<extension>"
     (bundled charts resolve audio by full filename against the Tracks resource namespace - see
     ResourceChartSource).
  3. Copies the jacket image (if any) into Garbus.Resources\Textures\Jackets, renamed to
     "<folder name>.<extension>".
  4. Rewrites metadata.audioFile and metadata.backgroundFile in the copied .garbus file to the new
     track/jacket filenames.

Only runs if google.com is reachable, since the source folders live on a network drive (Google Drive).
#>

$ErrorActionPreference = 'Stop'

$sourceRoot = 'G:\My Drive\Garbus\Charts'
$allowedListPath = Join-Path $sourceRoot 'AllowedCharts.txt'
$repoRoot = Split-Path -Parent $PSScriptRoot
$chartsDest = Join-Path $repoRoot 'Garbus.Resources\Charts'
$tracksDest = Join-Path $repoRoot 'Garbus.Resources\Tracks'
$jacketsDest = Join-Path $repoRoot 'Garbus.Resources\Textures\Jackets'
$audioExtensions = @('.mp3', '.ogg', '.wav')
$imageExtensions = @('.png', '.jpg', '.jpeg')

if (-not (Test-Connection -ComputerName 'google.com' -Count 1 -Quiet)) {
    Write-Error 'Ping to google.com failed; aborting import (source drive is likely unavailable).'
    exit 1
}

if (-not (Test-Path $allowedListPath)) {
    Write-Error "Allowed chart list not found: $allowedListPath"
    exit 1
}

New-Item -ItemType Directory -Path $chartsDest -Force | Out-Null
New-Item -ItemType Directory -Path $tracksDest -Force | Out-Null
New-Item -ItemType Directory -Path $jacketsDest -Force | Out-Null

$folderNames = Get-Content -Path $allowedListPath | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }

foreach ($folderName in $folderNames) {
    $sourceFolder = Join-Path $sourceRoot $folderName
    if (-not (Test-Path $sourceFolder -PathType Container)) {
        Write-Warning "Skipping '$folderName': folder not found at $sourceFolder"
        continue
    }

    Write-Host "Importing '$folderName'..."

    $chartFile = Get-ChildItem -Path $sourceFolder -Filter '*.garbus' -File | Select-Object -First 1
    if (-not $chartFile) {
        Write-Warning "Skipping '$folderName': no .garbus file found in $sourceFolder"
        continue
    }

    $trackFile = Get-ChildItem -Path $sourceFolder -File |
        Where-Object { $audioExtensions -contains $_.Extension.ToLowerInvariant() } |
        Select-Object -First 1

    if (-not $trackFile) {
        Write-Warning "Skipping '$folderName': no audio file found in $sourceFolder"
        continue
    }

    $newTrackName = "$folderName$($trackFile.Extension.ToLowerInvariant())"
    Copy-Item -Path $trackFile.FullName -Destination (Join-Path $tracksDest $newTrackName) -Force

    $imageFile = Get-ChildItem -Path $sourceFolder -File |
        Where-Object { $imageExtensions -contains $_.Extension.ToLowerInvariant() } |
        Select-Object -First 1

    $newJacketName = $null
    if ($imageFile) {
        $newJacketName = "$folderName$($imageFile.Extension.ToLowerInvariant())"
        Copy-Item -Path $imageFile.FullName -Destination (Join-Path $jacketsDest $newJacketName) -Force
    }
    else {
        Write-Warning "No image file found in $sourceFolder; leaving backgroundFile unchanged."
    }

    $destChart = Join-Path $chartsDest "$folderName.garbus"
    Copy-Item -Path $chartFile.FullName -Destination $destChart -Force

    $json = Get-Content -Path $destChart -Raw | ConvertFrom-Json
    $json.resources.track = $newTrackName
    if ($newJacketName) {
        $json.resources.background = $newJacketName
    }
    $json | ConvertTo-Json -Depth 20 | Set-Content -Path $destChart -Encoding utf8

    Write-Host "  -> chart at $destChart, track renamed to $newTrackName$(if ($newJacketName) { ", jacket renamed to $newJacketName" })"
}

Write-Host 'Done.'
