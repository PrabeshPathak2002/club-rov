[CmdletBinding()]
param(
    [switch]$ConnectUnityEditor,
    [switch]$ExternalNavigation,
    [switch]$Mapping,
    [switch]$Prequal,
    [switch]$CeruleanDvl,
    [ValidateNotNullOrEmpty()][string]$Distribution = 'Ubuntu-22.04',
    [string]$UnityCliPath
)
$ErrorActionPreference = 'Stop'
if ($Mapping -and !$ExternalNavigation) { throw '-Mapping requires -ExternalNavigation.' }
if ($Prequal -and $ExternalNavigation) { throw '-Prequal uses the standard profile; omit -ExternalNavigation.' }
if ($CeruleanDvl -and !$Prequal) { throw '-CeruleanDvl currently requires -Prequal.' }
$withRos = $ExternalNavigation -or $Prequal
$sessionMutex = [Threading.Mutex]::new($false, 'Local\ClubRovSitlSession')
$ownsMutex = $false
$ownsSession = $false
$profile = if ($ExternalNavigation) { 'external-nav' } elseif ($CeruleanDvl) { 'dvl' } else { 'standard' }
$children = [Collections.Generic.List[Diagnostics.Process]]::new()
$logStreams = [Collections.Generic.List[IDisposable]]::new()
$logTasks = [Collections.Generic.List[Threading.Tasks.Task]]::new()
$logFolder=Join-Path $PSScriptRoot 'logs'
New-Item -ItemType Directory -Path $logFolder -Force | Out-Null
$pythonPath = (Get-Command python -ErrorAction Stop).Source
$unityProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../ClubROVSim'))
$linuxScripts = '/mnt/' + $PSScriptRoot.Substring(0,1).ToLower() + $PSScriptRoot.Substring(2).Replace('\','/')
$unityCli = $null
if ($ConnectUnityEditor) {
    if ($UnityCliPath) {
        if (!(Test-Path -LiteralPath $UnityCliPath -PathType Leaf)) {
            throw "Unity CLI was not found at $UnityCliPath. Omit -ConnectUnityEditor for manual connection."
        }
        $unityCli = (Resolve-Path -LiteralPath $UnityCliPath).Path
    } else {
        $cliCommand = Get-Command unity.exe -CommandType Application -ErrorAction SilentlyContinue
        if ($cliCommand) { $unityCli = $cliCommand.Source }
        if (!$unityCli) {
            $defaultCli = Join-Path $env:LOCALAPPDATA 'Unity/bin/unity.exe'
            if (Test-Path -LiteralPath $defaultCli -PathType Leaf) { $unityCli = $defaultCli }
        }
        if (!$unityCli) {
            Write-Warning 'Unity CLI is unavailable. Starting SITL for manual connection; press CONNECT SITL in Unity. For external navigation, also start the ROS2 sensor stream while disarmed.'
            $ConnectUnityEditor = $false
        }
    }
}
function Start-Helper([string]$Executable, [string[]]$Arguments) {
    $info = [Diagnostics.ProcessStartInfo]::new($Executable)
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.WindowStyle = 'Hidden'
    $info.RedirectStandardOutput=$true
    $info.RedirectStandardError=$true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($info)
    $number=$children.Count
    foreach($kind in @('stdout','stderr')) {
        $path=Join-Path $logFolder "$number-$kind.log"
        $output=[IO.FileStream]::new($path,[IO.FileMode]::Create,[IO.FileAccess]::Write,[IO.FileShare]::ReadWrite,1,[IO.FileOptions]::Asynchronous)
        $logStreams.Add($output)
        $source=if($kind -eq 'stdout') {$process.StandardOutput.BaseStream} else {$process.StandardError.BaseStream}
        $logTasks.Add($source.CopyToAsync($output))
    }
    $children.Add($process)
    return $process
}
try {
    try { $ownsMutex = $sessionMutex.WaitOne(0) }
    catch [Threading.AbandonedMutexException] { $ownsMutex = $true }
    if (!$ownsMutex) { throw 'Another Club ROV SITL launcher is already running. Stop that session first.' }
    $homeOutput = & wsl -d $Distribution -- printenv HOME
    if ($LASTEXITCODE) { throw "Cannot access WSL distribution $Distribution." }
    $linuxHome = ($homeOutput -join '').Trim()
    & wsl -d $Distribution -- test -x "$linuxHome/club-rov-sitl/ardupilot/build/sitl/bin/ardusub"
    if ($LASTEXITCODE) { throw "Run setup-sitl.sh in $Distribution first." }
    $listeners = & wsl -d $Distribution -- ss -H -ltn
    if ($LASTEXITCODE) { throw 'Could not inspect the Linux SITL ports.' }
    $occupied = $listeners | Where-Object { $_ -match ':(5760|5765|9003)\s' }
    if ($occupied) { throw "SITL ports are already in use in $Distribution. Stop the existing session first." }
    if ($withRos) {
        & wsl -d $Distribution -- test -x "$linuxHome/club-rov-ros2-ws/mavlink-venv/bin/python"
        if ($LASTEXITCODE) { throw 'Run ROS2/setup-external-nav.sh and ROS2/build-workspace.sh first.' }
        if ($Mapping) {
            & wsl -d $Distribution -- test -f /opt/ros/humble/share/rtabmap_launch/launch/rtabmap.launch.py
            if ($LASTEXITCODE) { throw 'Install ros-humble-rtabmap-ros in the selected WSL distribution first.' }
        }
        $rosListeners = & wsl -d $Distribution -- ss -H -ltnu
        if ($rosListeners | Where-Object { $_ -match ':(9011|14553)\s' }) { throw 'A ROS sensor/navigation session is already running. Stop it before using the combined launcher.' }
        if (($Mapping -or $Prequal) -and ($rosListeners | Where-Object { $_ -match ':9013\s' })) { throw 'The RGB-D mapping receiver is already running. Stop it before using the combined launcher.' }
        if (Get-NetUDPEndpoint -LocalPort 9010 -ErrorAction SilentlyContinue) { throw 'The Windows Unity sensor relay is already running. Stop it before using the combined launcher.' }
    }
    $ownsSession = $true
    Start-Helper wsl @('-d',$Distribution,'--','bash',"$linuxScripts/start-relay.sh",$profile) | Out-Null
    Start-Sleep -Milliseconds 600
    Start-Helper $pythonPath @((Join-Path $PSScriptRoot 'json-relay.py'),'--client') | Out-Null
    Start-Helper wsl @('-d',$Distribution,'--','bash',"$linuxScripts/start-sitl.sh",'127.0.0.1',$profile) | Out-Null
    Start-Helper wsl @('-d',$Distribution,'--','bash',"$linuxScripts/start-mavproxy.sh",'127.0.0.1',$profile) | Out-Null
    if ($withRos) {
        $rosScript = if ($Prequal) { 'start-prequal-sensors.sh' } else { 'start-external-nav.sh' }
        $rosArgs = @('-d',$Distribution,'--','bash',"$linuxScripts/$rosScript")
        if ($Prequal) { $rosArgs += $profile }
        if ($Mapping) { $rosArgs += 'with_mapping:=true' }
        Start-Helper wsl $rosArgs | Out-Null
        Start-Helper $pythonPath @((Join-Path $PSScriptRoot '../../ROS2/tools/relay-unity-sensors.py')) | Out-Null
    }
    if ($ConnectUnityEditor) {
        $code = 'var b=UnityEngine.Object.FindAnyObjectByType<ClubROV.Simulation.RovSitlBridge>(); if(b==null || !UnityEngine.Application.isPlaying) throw new System.Exception("Enter PoolTraining Play Mode first."); var s=b.GetComponent<ClubROV.Simulation.RovDvlSensor>(); s?.StopStreaming(); b.Disconnect();'
        # Gate & pillar map, with the marker painted orange so the camera can pick it out of the cyan pool.
        if ($Prequal) { $code += ' b.GetComponent<ClubROV.Simulation.RovTrainingSettings>().SelectMap(1); b.GetComponent<ClubROV.Simulation.RovTrainingMaps>().CopyLayouts()[1].marker.GetComponent<UnityEngine.Renderer>().material.SetColor("_BaseColor", new UnityEngine.Color(1f,.35f,.05f));' }
        $code += ' b.GetComponent<ClubROV.Simulation.RovDynamics>().ResetVehicle(); b.Configure("127.0.0.1"); b.Connect();'
        if ($withRos) { $code += ' if(s==null) throw new System.Exception("Install the ROS2 navigation sensor component first."); s.StartStreaming();' }
        if ($Prequal) { $code += ' var rig=UnityEngine.Object.FindAnyObjectByType<ClubROV.Simulation.RovCameraRig>(); var f=rig.GetComponent<ClubROV.Simulation.RovMappingFeed>(); if(f==null) f=rig.gameObject.AddComponent<ClubROV.Simulation.RovMappingFeed>(); f.Configure(s); f.enabled=true; rig.SetView(ClubROV.Simulation.RovCameraRig.ViewMode.Fpv);' }
        $code += ' return b.Status;'
        & $unityCli command script eval --code $code --caller plugin --skill unity-feature-implementation --project-path $unityProject --format json
        if ($LASTEXITCODE) { throw 'Unity connection failed.' }
    }
    Write-Host 'Unity: Play PoolTraining, keep SITL IPv4 at 127.0.0.1, then CONNECT SITL.'
    Write-Host 'QGroundControl: TCP 127.0.0.1:5765. Vehicle SYSID must be 42.'
    Write-Host 'Use QGC joystick controls in SITL mode. Local Unity controls return after DISCONNECT.'
    Write-Host "WSL environment: $Distribution"
    Write-Host "SITL profile: $profile"
    if ($ExternalNavigation) {
        Write-Host 'ROS estimator, MAVLink odometry and sensor relay started. Wait for EKF position before choosing Position Hold.'
        if ($Mapping) { Write-Host 'RTAB-Map started. In Unity, select CONTROLS > START RGB-D MAPPING to begin capturing in FPV.' }
        if (!$ConnectUnityEditor) { Write-Host 'Start the Unity ROS2 sensor stream while disarmed, after connecting SITL.' }
    }
    if ($Prequal) {
        Write-Host 'Prequal camera pipeline started (no RTAB-Map). Disable the QGC joystick, then run ROS2/Run-Prequal-Mission.bat.'
        if ($CeruleanDvl) { Write-Host 'Simulated Tracker 650 sends VISION_POSITION_DELTA to ArduSub; wait for EKF position before starting the mission.' }
        if (!$ConnectUnityEditor) { Write-Host 'In Unity: select Gate & pillar before CONNECT SITL, start the ROS2 sensor stream, then CONTROLS > START RGB-D MAPPING in FPV.' }
    }
    Write-Host "Helper logs: $logFolder"
    [void](Read-Host 'Press Enter to stop this simulation session')
} finally {
    if ($ownsSession -and $unityCli -and $withRos) {
        # Stop Unity sending before the ROS receivers disappear.
        $stopCode = 'var b=UnityEngine.Object.FindAnyObjectByType<ClubROV.Simulation.RovSitlBridge>(); if(b==null || !UnityEngine.Application.isPlaying) return "not playing"; var rig=UnityEngine.Object.FindAnyObjectByType<ClubROV.Simulation.RovCameraRig>(); var f=rig==null ? null : rig.GetComponent<ClubROV.Simulation.RovMappingFeed>(); if(f!=null) f.enabled=false; var s=b.GetComponent<ClubROV.Simulation.RovDvlSensor>(); if(s!=null) s.StopStreaming(); b.Disconnect(); return "stopped";'
        & $unityCli command script eval --code $stopCode --caller plugin --skill unity-feature-implementation --project-path $unityProject --format json | Out-Null
    }
    if ($ownsSession) { & wsl -d $Distribution -- bash "$linuxScripts/stop-session.sh" $profile }
    foreach ($child in $children) {
        if (!$child.HasExited) { $child.Kill($true) }
        $child.Dispose()
    }
    foreach($task in $logTasks) {try {[void]$task.Wait(1000)} catch {}}
    foreach($stream in $logStreams) {$stream.Dispose()}
    if ($ownsMutex) { $sessionMutex.ReleaseMutex() }
    $sessionMutex.Dispose()
}
