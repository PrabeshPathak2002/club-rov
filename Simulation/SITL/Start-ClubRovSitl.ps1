[CmdletBinding()]
param([switch]$ConnectUnityEditor)
$ErrorActionPreference = 'Stop'
$children = [Collections.Generic.List[Diagnostics.Process]]::new()
$pythonPath = (Get-Command python -ErrorAction Stop).Source
$unityProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../ClubROVSim'))
$linuxScripts = '/mnt/' + $PSScriptRoot.Substring(0,1).ToLower() + $PSScriptRoot.Substring(2).Replace('\','/')
$unityCli = Join-Path $env:LOCALAPPDATA 'Unity/bin/unity.exe'
$linuxHome = (& wsl -d Ubuntu -- printenv HOME).Trim()
function Start-Helper([string]$Executable, [string[]]$Arguments) {
    $info = [Diagnostics.ProcessStartInfo]::new($Executable)
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.WindowStyle = 'Hidden'
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($info)
    $children.Add($process)
    return $process
}
try {
    & wsl -d Ubuntu -- test -x "$linuxHome/club-rov-sitl/ardupilot/build/sitl/bin/ardusub"
    if ($LASTEXITCODE) { throw 'Run setup-sitl.sh in Ubuntu first.' }
    Start-Helper wsl @('-d','Ubuntu','--',"$linuxHome/club-rov-sitl/venv/bin/python","$linuxScripts/json-relay.py",'--server') | Out-Null
    Start-Sleep -Milliseconds 600
    Start-Helper $pythonPath @((Join-Path $PSScriptRoot 'json-relay.py'),'--client') | Out-Null
    Start-Helper wsl @('-d','Ubuntu','--','bash',"$linuxScripts/start-sitl.sh",'127.0.0.1') | Out-Null
    Start-Helper wsl @('-d','Ubuntu','--','bash',"$linuxScripts/start-mavproxy.sh",'127.0.0.1') | Out-Null
    if ($ConnectUnityEditor) {
        $code = 'var b=UnityEngine.Object.FindAnyObjectByType<ClubROV.Simulation.RovSitlBridge>(); if(b==null) throw new System.Exception("Enter PoolTraining Play Mode first."); b.Disconnect(); b.GetComponent<ClubROV.Simulation.RovDynamics>().ResetVehicle(); b.Configure("127.0.0.1"); b.Connect(); return b.Status;'
        & $unityCli command script eval --code $code --caller plugin --skill unity-feature-implementation --project-path $unityProject --format json
        if ($LASTEXITCODE) { throw 'Unity connection failed.' }
    }
    Write-Host 'Unity: Play PoolTraining, keep SITL IPv4 at 127.0.0.1, then CONNECT SITL.'
    Write-Host 'QGroundControl: TCP 127.0.0.1:5765. Vehicle SYSID must be 42.'
    Write-Host 'Use QGC joystick controls in SITL mode. Local Unity controls return after DISCONNECT.'
    [void](Read-Host 'Press Enter to stop this simulation session')
} finally {
    & wsl -d Ubuntu -- bash "$linuxScripts/stop-session.sh"
    foreach ($child in $children) {
        if (!$child.HasExited) { $child.Kill($true) }
        $child.Dispose()
    }
}
