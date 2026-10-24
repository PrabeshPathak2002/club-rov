# Simulator validation — 23 October 2026

I checked the initial PoolTraining prototype in Unity 6000.6.7f1 on Windows. The fresh template started with no Console errors or warnings.

## Editor checks

- Runtime and Editor scripts compiled successfully.
- Six mixer checks passed: neutral, forward, sideways, vertical, yaw and bounded combined commands.
- The imported assembly has 247,680 vertices and visual bounds of approximately 0.38 × 0.26 × 0.36 m.
- The saved startup scene is `Assets/Scenes/PoolTraining.unity`, using the PC URP asset, a valid UI Toolkit document/PanelSettings and the imported mesh.
- I inspected a rendered Game view with the assembly, pool, gate and HUD. Play Mode frame counts advanced between checks.

## Runtime checks

All nine bounded Play Mode checks passed after correcting the reverse-thrust balance:

1. Neutral: no movement or velocity.
2. Forward: positive forward displacement without sideways motion.
3. Reverse: negative forward displacement.
4. Strafe: positive sideways displacement without forward drift.
5. Yaw: rotation without translation.
6. Ascend: positive vertical displacement.
7. Thrust cut: all six force commands clear; inertia remains.
8. Floor collision: the hull stays above the floor and downward velocity stops.
9. Reset: initial position and zero velocity are restored.

The retained [smoke-check results](smoke-checks.json) contain the final observations. The tests disable the pilot-input component, inject simulation commands and restore input/vehicle state afterward. They do not prove physical keyboard or controller operation.

The first run exposed sideways drift during yaw because reverse thrust was weaker. The force mixer now accounts for that limit before applying the common command scale. I kept the original pass criteria and reran the checks. A deprecated Editor-only lookup was also replaced with the current Unity API.

## Windows build

The final Windows x64 build succeeded with zero errors and zero warnings. The local executable is `Builds/Windows/ClubROVSim.exe`; its accompanying files must stay in the same folder. I have not separately launch-tested the standalone executable.

## Limits

Mass, displacement, drag, inertia, thruster positions and thrust curves are provisional. Passing these checks establishes the prototype's internal behavior, not agreement with the real ROV. Physical gamepad input, camera-switch key presses, long-duration operation and frame-rate performance remain to be checked interactively.

There is no ROS, SITL, MAVLink, tether or real-hardware connection in this prototype. The simulation cannot command the ESP32 or ARK.
