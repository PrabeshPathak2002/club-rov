param(
    [string]$ProjectPath = (Split-Path $PSScriptRoot -Parent)
)

$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$packageName = 'com.unity.ai.inference'
$manifest = Get-Content -LiteralPath (Join-Path $project 'Packages/manifest.json') -Raw | ConvertFrom-Json
$requiredVersion = $manifest.dependencies.$packageName
$destination = Join-Path $project "Packages/$packageName"
$workingPath = $destination

if (-not (Test-Path -LiteralPath $destination)) {
    $cache = Join-Path $project 'Library/PackageCache'
    $candidates = @(Get-ChildItem -LiteralPath $cache -Directory -Filter "$packageName@*" | Where-Object {
        $package = Get-Content -LiteralPath (Join-Path $_.FullName 'package.json') -Raw | ConvertFrom-Json
        $package.version -eq $requiredVersion
    })
    if ($candidates.Count -ne 1) {
        throw "Open the project in Unity once to resolve Inference Engine $requiredVersion, close Unity, then rerun this script."
    }
    $workingPath = "$destination.prepare-$([guid]::NewGuid().ToString('N'))"
    Copy-Item -LiteralPath $candidates[0].FullName -Destination $workingPath -Recurse
}

$installed = Get-Content -LiteralPath (Join-Path $workingPath 'package.json') -Raw | ConvertFrom-Json
if ($installed.version -ne $requiredVersion) {
    throw "The embedded package version differs from manifest.json. Preserve it and resolve the version mismatch before continuing."
}

$metadataPath = Join-Path $workingPath 'Editor/ONNX/Google.Protobuf_Packed.dll.meta'
$metadata = Get-Content -LiteralPath $metadataPath -Raw
if ($metadata -notmatch '(?ms)^  platformData:\r?\n.*?(?=^  userData:)') {
    throw 'The package importer metadata format has changed. Review the plugin settings manually.'
}
$platforms = @'
  platformData:
  - first:
      Any:
    second:
      enabled: 0
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: 1
      settings:
        CPU: AnyCPU
        DefaultValueInitialized: true
        OS: AnyOS

'@
$updated = [regex]::Replace($metadata, '(?ms)^  platformData:\r?\n.*?(?=^  userData:)', $platforms.Replace("`r`n", "`n"))
[System.IO.File]::WriteAllText($metadataPath, $updated, [System.Text.UTF8Encoding]::new($false))
if ($workingPath -ne $destination) {
    $packagesRoot = (Resolve-Path -LiteralPath (Join-Path $project 'Packages')).Path
    if ((Split-Path $workingPath -Parent) -ne $packagesRoot -or (Split-Path $destination -Parent) -ne $packagesRoot) {
        throw 'The prepared package paths must stay inside this project Packages directory.'
    }
    Move-Item -LiteralPath $workingPath -Destination $destination
}
Write-Host "Prepared Inference Engine $requiredVersion. Reopen Unity before building the Windows player."
Write-Host 'The embedded dependency stays local and is excluded from Git.'
