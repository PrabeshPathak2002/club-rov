# ArduSub and Unity

ArduSub 4.7.2 runs in Ubuntu 22.04 under WSL2, alongside ROS 2 Humble, with Unity providing the ROV physics and sensor backend. This is separate from the physical ARK. The simulator uses MAVLink system ID **42** and the six-motor **VECTORED** frame (`FRAME_CONFIG=1`). These parameter files are for SITL only.

## Prerequisites

Prepare these dependencies on the Windows computer that will run the simulation:

- **WSL2 with Ubuntu 22.04 registered as `Ubuntu-22.04`.** This is the launcher's default distribution and matches the ROS 2 Humble environment. Use the PowerShell `-Distribution` argument for another installed distribution with a prepared SITL environment.
- **Python 3 on Windows**, available as `python` on PATH, for the Windows relay.
- **PowerShell 7**, available as `pwsh`, for the launcher.
- **QGroundControl**, installed separately, with a joystick configured for piloting.
- The Unity project running `Assets/Scenes/PoolTraining.unity`, or a complete Windows simulator build. See the [simulator guide](../ClubROVSim/README.md).

The `.bat` launcher automatically connects the Unity Editor through the **Unity CLI** on PATH (or at `%LOCALAPPDATA%\Unity\bin\unity.exe`) and the project's existing **Unity Pipeline** package. An alternate CLI location can be supplied with `-UnityCliPath`. If the CLI is missing, the launcher warns and starts the stack for manual connection instead. For a standalone player, or if Editor automation is unavailable, use the manual connection command below instead.

## Build the Ubuntu tools

In Ubuntu 22.04, change to this checkout's `Simulation/SITL` directory. For a checkout on a Windows drive, use its WSL-mounted path. Install the build dependencies, then run the setup script with Bash:

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

ArduSub sends PWM actuator packets to the WSL relay on UDP 9002. A loopback TCP tunnel on 9003 crosses WSL's localhost forwarding. The Windows relay passes packets to Unity's UDP 9002 listener, and returns JSON sensor replies to ArduSub. MAVProxy routes simulator MAVLink to QGC on TCP 5765 and to the optional validation script on UDP 14551. A read-only heartbeat observer on WSL UDP 14552 sends arming/mode metadata through the same tunnel to Unity's loopback UDP 9004 listener. The badge becomes unknown after three seconds without a fresh heartbeat; it never infers arming from motor PWM. The heartbeat observer starts with the launcher; restart a running session after updating its relay tools.

Unity steps PhysX once per new actuator packet, returns IMU specific force and angular rates, NED position/velocity, attitude, and a fixed nominal battery reading. The JSON backend starts at 1200 Hz and settles to 400 Hz. Rendering runs separately. The HUD labels the requested step rate separately from measured real-time speed. Simulation time can still lag wall time when the available CPU/network budget is insufficient; this is not a hard real-time or calibrated hydrodynamic model.

The mapping from ArduSub motors 1–6 to Unity's H1/H2/H3/H4/V1/V2 is H2−, H1−, H4+, H3+, V2−, V1−. It follows the VECTORED mixer and the measured simulation geometry. It does not establish the physical ROV's motor wiring or propeller directions.

The listener accepts only the configured source IP and locks to its first valid peer. Duplicate packets resend the previous sensor reply without stepping physics. A one-second link timeout clears thrust and holds physics until an explicit reconnect. Local and SITL inputs cannot command the vehicle simultaneously.

The Windows launcher permits one session at a time and checks the selected distribution's SITL TCP ports before starting. Linux helper PID files identify the relay, ArduSub and MAVProxy processes for shutdown. Stopping a session verifies each PID's command line before termination; it does not kill unrelated processes by name. Disconnect Unity after stopping the launcher, as described above.

## Build outputs and local data

[setup-sitl.sh](setup-sitl.sh) pins ArduPilot to `fb773567896014da007bca58358704d4a0d8516f` (`Sub-4.7.2`) and creates an isolated Python **3.12.15** environment at `~/club-rov-sitl/venv`. ArduPilot source, the compiled binary, EEPROM and Linux-side logs stay under `~/club-rov-sitl`, outside Git. Windows helper logs are written to this directory's ignored `logs/` folder. Rerun the setup script when rebuilding the pinned tools.

ROS 2 Humble uses Ubuntu's Python 3.10. Its workspace and packages remain separate from the SITL Python 3.12 environment even though both run in the same distribution. Do not install ROS packages into the SITL virtual environment. The [ROS 2 sensor pipeline](../../ROS2/README.md) uses separate ports 9010/9011. Its optional external-navigation profile supplies DVL-assisted position to ArduSub over loopback UDP 14553.

## DVL navigation profile

After the [ROS external-navigation setup](../../ROS2/README.md#dvl-assisted-ardusub-sitl-and-qgc), use [Start-ClubRovSitl-ExternalNav.bat](Start-ClubRovSitl-ExternalNav.bat) in Unity Play Mode. This combined launcher starts ROS, the estimator, odometry sender and sensor relay, and automatically starts the Unity sensor stream. Wait for valid EKF position before selecting **Position Hold** in QGC; connect to the same TCP port 5765 and SYSID 42.

The profile loads [external-nav.parm](external-nav.parm), retaining compass yaw and pressure depth while fusing external horizontal position/velocity. Pinned Sub-4.7.2 uses `VISO_TYPE=1` for MAVLink. A separate `~/club-rov-sitl/run-external-nav` EEPROM/log directory preserves the normal session's settings. Existing EEPROM values override source defaults. The ROS bridge initializes a synthetic geographic origin matching the existing SITL home; QGC coordinates do not represent the actual pool location. ROS estimates use DVL/IMU/depth, not simulator pose truth.

The optional bridge is restricted to the isolated simulator and does not arm or send actuator commands. Disarm and restart the full session after a reset/map change or navigation fault. Bottom-lock loss withholds new odometry; physical navigation and autonomous failure recovery remain unvalidated.

When migrating an existing installation, copy the pinned source and `run/eeprom.bin`, rebuild with `setup-sitl.sh` in the destination, and retain the original distribution until validation passes. Do not reuse the old distribution's compiled binary or virtual environment. To select an older prepared distribution explicitly, run `pwsh -File ./Start-ClubRovSitl.ps1 -Distribution Ubuntu -ConnectUnityEditor` from this directory. Avoid running two distributions' SITL sessions simultaneously because their Windows-facing ports are shared.

## Validation

Local validation built ArduSub 4.7.2 and verified live 400 Hz packet exchange, firmware version, the six-motor frame, arming, neutral/forward/starboard/right-yaw/up outputs and disarming. The Unity vehicle moved from its initial 1.20 m depth to about 0.72 m, moved forward and starboard, and turned right. The test ended with all six outputs at 1500 µs. The generated MAVLink report was retained locally; [check-mavlink.py](check-mavlink.py) remains available to reproduce the guarded simulator check.

The focused Unity checks passed: 12 protocol, 12 ownership/UDP/timeout lifecycle, 11 local physics, 12 synthetic input/camera, 19 course/UI/visibility, and 10 FPV rendering checks. Synthetic input checks require Unity to have OS focus. The later hands-on QGC joystick, depth-hold and heading-hold checks were confirmed separately.

Absolute depth calibration, autopilot tuning, camera streaming into QGC, and the real ROV are not validated by this integration. Mass, buoyancy, drag, inertia and thrust remain provisional until the assembled vehicle is measured.

The optional external-navigation profile passed a subsequent live check: QGC-compatible global/local position messages, accepted Position Hold, 0.012 m maximum excursion over eight simulation seconds in a 0.2 m/s cross-current, pilot repositioning and a 0.005 m/s final horizontal speed. [check-external-nav.py](check-external-nav.py) reproduces the guarded control check and always disarms on normal completion or a Python exception. DVL dropout withheld odometry. These results validate the short simulation loop, not physical performance.

Ubuntu 22.04 migration validation rebuilt the same pinned revision, preserved the pre-launch EEPROM contents, and verified the 400 Hz actuator loop. The guarded SYSID 42 motor check passed neutral, forward, starboard, yaw and ascent phases and ended disarmed with six neutral outputs. Windows received the submarine heartbeat on TCP 5765. The ROS 2 DVL/IMU/local-odometry pipeline also passed its stationary live check while ArduSub owned Unity physics. Duplicate-launch protection, stale-PID identity checks, helper shutdown and a subsequent clean restart were exercised. This validates co-running SITL and ROS, not ROS external-navigation feedback or physical hardware.

## Desktop timing

To measure the current session without arming, resetting, or changing parameters, run `pwsh -File ./Measure-ClubRovSitl.ps1 -Seconds 10` from this directory. It uses the existing local Unity Pipeline endpoint and does not require the Unity CLI. Enter Play Mode, connect SITL and wait for fresh SYSID 42 **DISARMED** telemetry first. Keep the usual Game view and ROS stream configuration for the sample. The result reports rendering FPS, physics steps per wall second and the ratio of simulation time to wall time. The bridge's displayed **400 Hz step** is the actuator packet's requested physics step rate, not measured throughput: 7 simulation seconds over 11 wall seconds is about **0.64x real time**, or 255 physics steps per wall second at a 400 Hz requested rate. The adjacent real-time factor is measured over half-second wall-time windows.

The lockstep pump now reserves 75% of the current render-frame interval, bounded between 10 and 25 ms, and waits for a reply only within that remaining allowance. It stops as soon as simulation catches wall time and retains the 64-packet work cap, duplicate handling, exclusive input/physics ownership and one-second lost-link watchdog. This replaces a fixed 10 ms allowance and an early exit after a single 1 ms receive timeout. It changes service time, not the physics timestep or the ArduSub scheduler.

On Z16P-PP on 31 October 2026, the original code measured 1.004x at 120 FPS and 1.027x at 60 FPS, so the originally reported 7/11 slowdown was not reproduced at those caps in the fresh session. Selecting the supported 30 FPS cap reproduced frame-dependent starvation: 0.422x at 28.4 rendered FPS (168.9 physics steps per wall second). After the pump change and a coordinated disarmed restart, with healthy ROS external-navigation odometry confirmed flowing to SITL, ten-second samples measured 1.003x at 30.0 FPS, 1.000x at 59.9 FPS and 1.000x at 116.0 FPS with the 120 cap. The original 120 cap was restored. The restart was concurrent with the code change, so these figures establish the tested configurations rather than a general latency guarantee. All 12 protocol, 12 socket/lifecycle and four timing-budget checks passed; all 26 ROS tests passed. No vehicle was armed for these checks.

The Windows sensor relay also tolerates UDP `ConnectionResetError` while Unity's return-telemetry listener restarts. Windows Python 3.11.9 does not expose `socket.SIO_UDP_CONNRESET`, so checking only for that optional constant did not protect this installation. A regression test confirms that sensor forwarding continues after the reset.

The desktop default uses a 200 Hz ArduSub scheduler (`SCHED_LOOP_RATE=200`) with the existing 400 Hz JSON physics backend. This reduces autopilot scheduling load while keeping the sensor/physics packet rate unchanged. Existing EEPROM settings override defaults: for an older simulator profile, set `SCHED_LOOP_RATE` to 200 in QGC and restart the complete session. These settings are for SYSID 42 SITL only.

A live comparison measured about 58 FPS with the sidebar visible and 60 FPS with the HUD hidden, while the 400 Hz scheduler session advanced at roughly 0.70 simulation seconds per wall second. After changing the scheduler to 200 Hz and restarting the stack, an eight-second sample measured 60 FPS, 20.6 ms 95th-percentile frame time and 1.00 simulation seconds per wall second. A neutral-input depth-hold check then varied by 0.017 m during its steady portion and ended disarmed. The restart and rate change were applied together, so this is integration evidence, not an isolated CPU benchmark.
