# ArduSub and Unity

I run ArduSub 4.7.2 in Ubuntu under WSL2 and use Unity as the ROV's physics and sensor backend. This is separate from the physical ARK. The simulator uses MAVLink system ID **42** and the six-motor **VECTORED** frame (`FRAME_CONFIG=1`). These parameter files are for SITL only.

## Start on this desktop

1. Open `ClubROVSim` in Unity and play `Assets/Scenes/PoolTraining.unity`.
2. In PowerShell 7, run:

   ```powershell
   cd 'C:\Users\drago\Documents\ROV project\ClubROV\Simulation\SITL'
   ./Start-ClubRovSitl.ps1 -ConnectUnityEditor
   ```

   Without `-ConnectUnityEditor`, press **CONNECT SITL** in the Game view. Keep its IPv4 field at `127.0.0.1`. The Windows player uses the same button.
3. In QGroundControl, connect the **Club ROV SITL ONLY** TCP link: `127.0.0.1:5765`. Confirm vehicle ID 42. I disabled USB, radio, UDP and ZeroConf auto-discovery in the local QGC profile. A backup of the previous profile is in the workspace's `Archive/SITLSetup` folder.
4. Use QGC's joystick controls for SITL; Unity's local pilot is disabled while ArduSub owns thrust. Camera and visibility controls remain in Unity. QGC joystick calibration and a hands-on QGC controller test are still required.
5. Disarm in QGC before ending a session. Press Enter in the launcher to stop its helpers. **DISCONNECT SITL** restores Unity's local controls. Restart both SITL and the Unity connection after a simulation reset, Editor pause/recompile, or lost link.

The official QGC 5.1.5 files are available locally at `Archive/SITLSetup/QGroundControl/bin/QGroundControl.exe` outside this repository. I used `--swrast` for its software-rendering startup. They are not included in Git. Unity's existing public-network firewall block was preserved; I added no firewall rules.

## How the connection works

ArduSub sends PWM actuator packets to the WSL relay on UDP 9002. A loopback TCP tunnel on 9003 crosses WSL's localhost forwarding. The Windows relay passes packets to Unity's UDP 9002 listener, and returns JSON sensor replies to ArduSub. MAVProxy routes simulator MAVLink to QGC on TCP 5765 and to the optional validation script on UDP 14551.

Unity steps PhysX once per new actuator packet, returns IMU specific force and angular rates, NED position/velocity, attitude, and a fixed nominal battery reading. The JSON backend starts at 1200 Hz and settles to 400 Hz. Rendering runs separately. Simulation time can lag wall time when the Editor or graphics load is high; this is not a real-time or calibrated hydrodynamic model.

The mapping from ArduSub motors 1–6 to Unity's H1/H2/H3/H4/V1/V2 is H2−, H1−, H4+, H3+, V2−, V1−. It follows the VECTORED mixer and the measured simulation geometry. It does not establish the physical ROV's motor wiring or propeller directions.

The listener accepts only the configured source IP and locks to its first valid peer. Duplicate packets resend the previous sensor reply without stepping physics. A one-second link timeout clears thrust and holds physics until an explicit reconnect. Local and SITL inputs cannot command the vehicle simultaneously.

## Rebuild the WSL tools

Ubuntu is already installed on this desktop. In a fresh Ubuntu environment, install the packages listed at the top of `setup-sitl.sh`, then run that script with Bash. It pins ArduPilot to `fb773567896014da007bca58358704d4a0d8516f` (`Sub-4.7.2`) and uses an isolated Python 3.12 environment at `~/club-rov-sitl/venv`. The source, compiled binary, EEPROM and logs stay under `~/club-rov-sitl`, outside Git. The Windows relay needs Python 3; the launcher needs PowerShell 7.

## Validation

On this desktop I built ArduSub 4.7.2 and verified the live 400 Hz packet exchange, firmware version, six-motor frame, arming, neutral/forward/starboard/right-yaw/up outputs and disarming. The Unity vehicle moved from its initial 1.20 m depth to about 0.72 m, moved forward and starboard, and turned right. The test ended with all six outputs at 1500 µs. `validation-mavlink.json` records the final six-motor run.

The focused Unity checks passed: 12 protocol, 12 ownership/UDP/timeout lifecycle, 11 local physics, 12 synthetic input/camera, 19 course/UI/visibility, and 10 FPV rendering checks. Synthetic input checks require Unity to have OS focus. These do not replace a hands-on QGC joystick test.

Depth hold, heading hold, absolute depth calibration, autopilot tuning, camera streaming into QGC, and the real ROV are not validated by this integration. Mass, buoyancy, drag, inertia and thrust remain provisional until I measure the assembled vehicle.
