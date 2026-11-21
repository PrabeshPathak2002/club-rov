[CmdletBinding()]
param(
    [ValidateRange(3, 60)][int]$Seconds = 10
)
$ErrorActionPreference = 'Stop'
# Read the existing Pipeline endpoint. No CLI install, arming, parameter writes,
# connection/reset, or simulation clock changes are performed by this probe.
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../ClubROVSim'))
$descriptorPath = Join-Path $project 'Library/Pipeline/.unity-pipeline-port'
function Get-TimingSnapshot {
    if (!(Test-Path -LiteralPath $descriptorPath)) { throw 'Open ClubROVSim in Unity and enter PoolTraining Play Mode first.' }
    $descriptor = Get-Content -LiteralPath $descriptorPath -Raw | ConvertFrom-Json
    if ([IO.Path]::GetFullPath($descriptor.projectPath) -ne $project) { throw 'Pipeline descriptor targets another project.' }
    $port = [int]$descriptor.port
    if ($port -lt 7800 -or $port -gt 7849) { throw 'Unexpected Pipeline Editor port.' }
    $code = @'
if (!UnityEngine.Application.isPlaying) throw new System.Exception("Enter Play Mode first.");
var b = UnityEngine.Object.FindAnyObjectByType<ClubROV.Simulation.RovSitlBridge>();
if (b == null || !b.Active) throw new System.Exception("Connect SITL first.");
if (!b.TelemetryFresh || b.Armed) throw new System.Exception("Timing test requires fresh, DISARMED SYSID 42 telemetry.");
return new { wall = UnityEngine.Time.realtimeSinceStartupAsDouble, simulation = b.SimulatedSeconds,
    steps = b.Steps, frame = UnityEngine.Time.frameCount, status = b.Status,
    sensors = b.GetComponent<ClubROV.Simulation.RovDvlSensor>()?.Streaming ?? false };
'@
    $body = @{
        command = 'script eval'
        parameters = @{ code = $code }
        clientSource = 'agent'
        clientName = 'club-rov-timing'
    } | ConvertTo-Json -Depth 5
    $reply = Invoke-RestMethod -Uri "http://127.0.0.1:$port/api/exec" -Method Post -ContentType 'application/json' -Headers @{ Authorization = "Bearer $($descriptor.evalToken)" } -Body $body -TimeoutSec 15
    if (!$reply.success) { throw ($reply | ConvertTo-Json -Depth 8) }
    # script eval wraps the evaluated return value in an EvalResponse.
    $result = $reply.result
    if ($result.PSObject.Properties.Name -contains 'success' -and !$result.success) { throw ($result | ConvertTo-Json -Depth 8) }
    if ($result.PSObject.Properties.Name -contains 'result') { $result = $result.result }
    if ($null -eq $result.wall -or $null -eq $result.steps) { throw "Unexpected Pipeline response: $($reply | ConvertTo-Json -Depth 8)" }
    return $result
}
$first = Get-TimingSnapshot
Write-Host "Measuring $Seconds seconds with SITL disarmed. Keep the usual Unity view and ROS configuration."
Start-Sleep -Seconds $Seconds
$last = Get-TimingSnapshot
$wall = $last.wall - $first.wall
$simulation = $last.simulation - $first.simulation
if ($wall -le 0 -or $simulation -lt 0 -or $last.steps -lt $first.steps) { throw 'Simulation stopped or reset during the sample.' }
if ($first.sensors -ne $last.sensors) { throw 'ROS streaming changed during the sample; repeat with a stable configuration.' }
[pscustomobject]@{
    WallSeconds = [math]::Round($wall, 3)
    SimulationSeconds = [math]::Round($simulation, 3)
    RealTimeFactor = [math]::Round($simulation / $wall, 3)
    RenderFps = [math]::Round(($last.frame - $first.frame) / $wall, 1)
    PhysicsStepsPerWallSecond = [math]::Round(($last.steps - $first.steps) / $wall, 1)
    RosSensorsStreaming = $last.sensors
    Status = $last.status
} | ConvertTo-Json
