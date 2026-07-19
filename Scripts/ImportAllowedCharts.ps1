<#
Imports charts approved in G:\My Drive\Garbus\AllowedCharts.txt into this repo's Garbus.Resources.

Each line in AllowedCharts.txt is the name of a folder under G:\My Drive\Garbus containing a chart
(one or more .garbus files + one audio track, e.g. "aerodynamic"). For each listed folder, this
script:
  1. Copies the folder as-is into Garbus.Resources\Charts.
  2. Moves the audio track out of that copy into Garbus.Resources\Tracks, renamed to
     "<folder name>.<extension>" (bundled charts resolve audio by full filename against the
     Tracks resource namespace, not a sibling file - see ResourceChartSource).
  3. Rewrites metadata.audioFile in every .garbus file in the copied folder to the new track
     filename.

Only runs if google.com is reachable, since the source folders live on a network drive (Google Drive).
#>

$ErrorActionPreference = 'Stop'

$sourceRoot = 'G:\My Drive\Garbus'
$allowedListPath = Join-Path $sourceRoot 'AllowedCharts.txt'
$repoRoot = Split-Path -Parent $PSScriptRoot
$chartsDest = Join-Path $repoRoot 'Garbus.Resources\Charts'
$tracksDest = Join-Path $repoRoot 'Garbus.Resources\Tracks'
$audioExtensions = @('.mp3', '.ogg', '.wav')

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

$folderNames = Get-Content -Path $allowedListPath | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }

foreach ($folderName in $folderNames) {
    $sourceFolder = Join-Path $sourceRoot $folderName
    if (-not (Test-Path $sourceFolder -PathType Container)) {
        Write-Warning "Skipping '$folderName': folder not found at $sourceFolder"
        continue
    }

    $destFolder = Join-Path $chartsDest $folderName
    Write-Host "Importing '$folderName'..."
    Copy-Item -Path $sourceFolder -Destination $destFolder -Recurse -Force

    $trackFile = Get-ChildItem -Path $destFolder -File |
        Where-Object { $audioExtensions -contains $_.Extension.ToLowerInvariant() } |
        Select-Object -First 1

    if (-not $trackFile) {
        Write-Warning "Skipping audio move for '$folderName': no audio file found in $destFolder"
        continue
    }

    $newTrackName = "$folderName$($trackFile.Extension.ToLowerInvariant())"
    $newTrackPath = Join-Path $tracksDest $newTrackName
    Move-Item -Path $trackFile.FullName -Destination $newTrackPath -Force

    Get-ChildItem -Path $destFolder -Filter '*.garbus' -File | ForEach-Object {
        $json = Get-Content -Path $_.FullName -Raw | ConvertFrom-Json
        $json.metadata.audioFile = $newTrackName
        $json | ConvertTo-Json -Depth 20 | Set-Content -Path $_.FullName -Encoding utf8
    }

    Write-Host "  -> chart(s) in $destFolder, track renamed to $newTrackName"
}

Write-Host 'Done.'
