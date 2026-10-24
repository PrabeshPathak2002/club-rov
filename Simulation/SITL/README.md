# ArduSub and Unity

ArduSub 4.7.2 runs in Ubuntu under WSL2, with Unity providing the ROV physics and sensor backend. This is separate from the physical ARK. The simulator uses MAVLink system ID **42** and the six-motor **VECTORED** frame (`FRAME_CONFIG=1`). These parameter files are for SITL only.

## Prerequisites

Prepare these dependencies on the Windows computer that will run the simulation:

- **WSL2 with an Ubuntu distribution registered as `Ubuntu`.** The launcher calls `wsl -d Ubuntu`; a differently named distribution requires updating those launcher references.
- **Python 3 on Windows**, available as `python` on PATH, for the Windows relay.
- **PowerShell 7**, available as `pwsh`, for the launcher.
- **QGroundControl**, installed separately, with a joystick configured for piloting.
- The Unity project running `Assets/Scenes/PoolTraining.unity`, or a complete Windows simulator build. See the [simulator guide](../ClubROVSim/README.md).

The `.bat` launcher automatically connects the Unity Editor through the **Unity CLI** at `%LOCALAPPDATA%\Unity\bin\unity.exe` and the project's existing **Unity Pipeline** package. For a standalone player, or if Editor automation is unavailable, use the manual connection command below instead.

## Build the Ubuntu tools

In Ubuntu, change to this checkout's `Simulation/SITL` directory. For a checkout on a Windows drive, use its WSL-mounted path. Install the build dependencies, then run the setup script with Bash:

```bash
sudo apt-get update
sudo apt-get install git build-essential python3-dev python3-venv pkg-config libtool libxml2-dev libxslt1-dev zlib1g-dev ccache rsync
bash ./setup-sitl.sh
```

The script downloads the pinned ArduPilot source and Python dependencies and builds ArduSub. It must finish successfully before launching a session. Ubuntu, QGC and these build outputs are not supplied by cloning the repository.

## Start a session

1. Start the simulator, select the map/start pose, and enter Play Mode if using the Editor.
2. Start SITL using one of the following options. Keep its terminal open throughout the session.

   **Editor auto-connect:** double-click [Start-ClubRovSitl.bat](Start-ClubRovSitl.bat). It finds PowerShell 7, bypasses the execution policy for this launch only, and requests connection to the running Unity scene. It does not change the saved execution policy.

   **Manual connection / standalone player:** from the repository root in PowerShell, run:

   ```powershell
   Set-Location Simulation/SITL
   pwsh -NoProfile -ExecutionPolicy Bypass -File ./Start-ClubRovSitl.ps1
   ```

   Then press **CONNECT SITL** in the simulator, with IPv4 set to `127.0.0.1`. Add `-ConnectUnityEditor` to the command only when using the Editor automation dependencies above.
3. In QGroundControl, create a **TCP** communication link to **127.0.0.1:5765**; a suggested link name is **Club ROV SITL ONLY**. Connect and confirm **vehicle ID 42**. The link is not created by cloning the repository. Disable unrelated automatic connections while testing the simulator.
4. Pilot with QGC's joystick controls. Unity's local pilot is disabled while ArduSub owns thrust; camera and visibility controls remain in Unity. **Arm in QGC before expecting thrust.**
5. Disarm in QGC before ending a session. Press Enter in the launcher terminal to stop its helpers. **DISCONNECT SITL** restores Unity's local controls. Restart SITL and the Unity connection after a simulation reset, Editor pause/recompile, map/start change or lost link.

## QGroundControl configuration

The recorded validation used **QGroundControl 5.1.5** with a TCP link and USB, radio, UDP and ZeroConf auto-discovery disabled. These are validation settings, not a profile installed automatically on another computer. The validation installation used `--swrast` for software rendering; this option is not required by the network protocol. QGC binaries and private profile backups are not distributed in this repository.

## How the connection works

ArduSub sends PWM actuator packets to the WSL relay on UDP 9002. A loopback TCP tunnel on 9003 crosses WSL's localhost forwarding. The Windows relay passes packets to Unity's UDP 9002 listener, and returns JSON sensor replies to ArduSub. MAVProxy routes simulator MAVLink to QGC on TCP 5765 and to the optional validation script on UDP 14551. A read-only heartbeat observer on WSL UDP 14552 sends arming/mode metadata through the same tunnel to Unity's loopback UDP 9004 listener. The badge becomes unknown after three seconds without a fresh heartbeat; it never infers arming from motor PWM. Restart the launcher to enable this new status path.

Unity steps PhysX once per new actuator packet, returns IMU specific force and angular rates, NED position/velocity, attitude, and a fixed nominal battery reading. The JSON backend starts at 1200 Hz and settles to 400 Hz. Rendering runs separately. Simulation time can lag wall time when the Editor or graphics load is high; this is not a real-time or calibrated hydrodynamic model.

The mapping from ArduSub motors 1–6 to Unity's H1/H2/H3/H4/V1/V2 is H2−, H1−, H4+, H3+, V2−, V1−. It follows the VECTORED mixer and the measured simulation geometry. It does not establish the physical ROV's motor wiring or propeller directions.

The listener accepts only the configured source IP and locks to its first valid peer. Duplicate packets resend the previous sensor reply without stepping physics. A one-second link timeout clears thrust and holds physics until an explicit reconnect. Local and SITL inputs cannot command the vehicle simultaneously.

## Build outputs and local data

[setup-sitl.sh](setup-sitl.sh) pins ArduPilot to `fb773567896014da007bca58358704d4a0d8516f` (`Sub-4.7.2`) and creates an isolated Python **3.12.15** environment at `~/club-rov-sitl/venv`. ArduPilot source, the compiled binary, EEPROM and Linux-side logs stay under `~/club-rov-sitl`, outside Git. Windows helper logs are written to this directory's ignored `logs/` folder. Rerun the setup script when rebuilding the pinned tools.

## Validation

Local validation built ArduSub 4.7.2 and verified live 400 Hz packet exchange, firmware version, the six-motor frame, arming, neutral/forward/starboard/right-yaw/up outputs and disarming. The Unity vehicle moved from its initial 1.20 m depth to about 0.72 m, moved forward and starboard, and turned right. The test ended with all six outputs at 1500 µs. `validation-mavlink.json` records the final six-motor run.

The focused Unity checks passed: 12 protocol, 12 ownership/UDP/timeout lifecycle, 11 local physics, 12 synthetic input/camera, 19 course/UI/visibility, and 10 FPV rendering checks. Synthetic input checks require Unity to have OS focus. The later hands-on QGC joystick, depth-hold and heading-hold checks were confirmed separately.

Absolute depth calibration, autopilot tuning, camera streaming into QGC, and the real ROV are not validated by this integration. Mass, buoyancy, drag, inertia and thrust remain provisional until the assembled vehicle is measured.
