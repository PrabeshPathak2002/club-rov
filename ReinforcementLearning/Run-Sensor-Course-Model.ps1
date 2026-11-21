param([int]$Episodes = 0, [double]$Speed = 1, [switch]$NoPreview, [int]$BasePort = 8500)
$ErrorActionPreference = 'Stop'
if (-not $NoPreview) {
    try {
        $existingRun = Invoke-RestMethod -Uri 'http://127.0.0.1:8766/status' -TimeoutSec 1
        if ($null -ne $existingRun.phase -and $null -ne $existingRun.episodes) {
            Start-Process 'http://127.0.0.1:8766/'
            exit 0
        }
    } catch { }
}
$linuxDirectory = (& wsl.exe -d Ubuntu-22.04 -u root -- wslpath -u $PSScriptRoot).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve the model runner path in WSL.' }
$runnerArgs = @('--episodes', "$Episodes", '--speed', $Speed.ToString([Globalization.CultureInfo]::InvariantCulture), '--base-port', "$BasePort")
if ($NoPreview) { $runnerArgs += '--no-preview' }
& wsl.exe -d Ubuntu-22.04 -u root -- bash "$linuxDirectory/run-sensor-course-model.sh" @runnerArgs
exit $LASTEXITCODE
