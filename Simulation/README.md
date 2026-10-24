# ROV simulation

[ClubROVSim](ClubROVSim/README.md) is the Unity pool-training simulator. Open that folder in Unity Hub with Unity 6000.6.7f1.

It includes a six-thruster model, pool and RoboSub practice maps, keyboard/gamepad controls, chase/FPV/top-down cameras, water-visibility settings and selectable FPV instruments. Physical coefficients remain provisional.

Local practice runs without QGroundControl. The optional [ArduSub SITL integration](SITL/README.md) uses the simulator as a physics backend for software-only autopilot practice. It does not connect to the physical vehicle.

The current compiled Windows player and shareable practice ZIP live in `ClubROVSim/Builds/`, excluded from Git. See the [folder guide](../Documentation/FOLDER_GUIDE.md) for generated files and archived snapshots.
