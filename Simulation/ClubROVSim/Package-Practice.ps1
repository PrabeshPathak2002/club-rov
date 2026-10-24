$ErrorActionPreference = 'Stop'
$build = Join-Path $PSScriptRoot 'Builds/Windows'
if (!(Test-Path (Join-Path $build 'ClubROVSim.exe'))) { throw 'Build the Windows player first.' }
$stage = Join-Path $PSScriptRoot ('Builds/Practice-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $stage | Out-Null
Get-ChildItem -LiteralPath $build | Where-Object { $_.Name -notmatch 'BackUpThisFolder|BurstDebugInformation' } | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $stage -Recurse
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Docs/PRACTICE_QUICKSTART.txt') -Destination (Join-Path $stage 'READ-ME-FIRST.txt')
Set-Content -LiteralPath (Join-Path $stage 'Start-Practice.bat') -Encoding ascii -Value '@echo off', 'cd /d "%~dp0"', 'start "" "ClubROVSim.exe" -screen-fullscreen 0 -screen-width 1280 -screen-height 720'
$zip = "$stage.zip"
Compress-Archive -LiteralPath $stage -DestinationPath $zip -CompressionLevel Optimal
Write-Output $zip
