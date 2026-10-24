# ROV simulation

[ClubROVSim](ClubROVSim/README.md) is the Unity simulator for six-thruster ROV pilot training. It includes six maps, adjustable water conditions, saved scenarios, course feedback and Chase/FPV/Top-down cameras. Telemetry panels minimize independently, and FPV instruments offer compact dials, a flight overlay and Betaflight-style OSD.

## Choose a control mode

- **Local practice:** use keyboard or gamepad directly in Unity or the Windows player. QGroundControl is not required.
- **ArduSub SITL:** run the software autopilot under WSL2/Ubuntu and pilot through QGroundControl. Unity supplies physics and sensor data. See the [SITL setup](SITL/README.md).

Open `ClubROVSim` in Unity Hub with **Unity 6000.6.7f1**, open `Assets/Scenes/PoolTraining.unity`, press Play and click the Game view. The [simulator README](ClubROVSim/README.md) covers controls, map selection, FPV/HUD settings and building a shareable Windows package.

Maps include the original pool course, gate/pillar practice, empty pool, slalom and RoboSub 2026 practice/finals layouts. The handbook maps use static task stations and estimated spacing; they do not implement official judging or complete task interactions. Physical coefficients remain provisional, and neither control mode connects to the real ROV.

Compiled Windows players and practice ZIPs live locally under `ClubROVSim/Builds/` and are excluded from Git. A fresh clone requires a Unity build or a complete practice package from the club. See the [folder guide](../Documentation/FOLDER_GUIDE.md) for source, generated outputs and archive retention.
