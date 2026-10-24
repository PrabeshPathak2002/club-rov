$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$build = Join-Path $PSScriptRoot 'Builds/Windows'
if (!(Test-Path -LiteralPath (Join-Path $build 'ClubROVSim.exe'))) { throw 'Build the Windows player first.' }
$zipPath = Join-Path $PSScriptRoot ('Builds/Practice-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.zip')
$partialPath = "$zipPath.partial"
$prefix = 'ClubROVSim-Windows/'
$stream = [System.IO.File]::Open($partialPath, [System.IO.FileMode]::CreateNew)
$archive = [System.IO.Compression.ZipArchive]::new($stream, [System.IO.Compression.ZipArchiveMode]::Create, $false)
try {
    # Write directly into the ZIP; no duplicate extracted player folder is left behind.
    foreach ($file in Get-ChildItem -LiteralPath $build -Recurse -File) {
        $relative = [System.IO.Path]::GetRelativePath($build, $file.FullName).Replace('\', '/')
        if ($relative -match '(^|/)[^/]*(BackUpThisFolder|BurstDebugInformation)[^/]*(/|$)') { continue }
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName,
            $prefix + $relative, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,
        (Join-Path $PSScriptRoot 'Docs/PRACTICE_QUICKSTART.txt'), $prefix + 'READ-ME-FIRST.txt',
        [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    $entry = $archive.CreateEntry($prefix + 'Start-Practice.bat')
    $writer = [System.IO.StreamWriter]::new($entry.Open(), [System.Text.UTF8Encoding]::new($false))
    try {
        $writer.NewLine = "`r`n"
        $writer.WriteLine('@echo off')
        $writer.WriteLine('cd /d "%~dp0"')
        $writer.WriteLine('start "" "ClubROVSim.exe" -screen-fullscreen 0 -screen-width 1280 -screen-height 720')
    } finally { $writer.Dispose() }
} finally { $archive.Dispose(); $stream.Dispose() }
# A failed run leaves a .partial file, never a falsely complete release ZIP.
Move-Item -LiteralPath $partialPath -Destination $zipPath
Write-Output $zipPath
