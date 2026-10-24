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

## Measured geometry and control revision

I integrated the live Fusion thrust-axis lines and envelope into the runtime layout, force markers, collider and partial-submersion height. A minimum-norm horizontal allocation accounts for the small differences in the measured yaw lever arms. The geometric reference is still a provisional center of mass.

Six mixer checks, **eleven Play Mode checks** and **twelve synthetic input/camera checks** passed. The retained [motion results](smoke-checks.json) and [control results](control-checks.json) cover movement, neutral, yaw, floor contact, reset, thrust cut, a 30% force cap, input-disable cleanup, keyboard directions, gamepad axes/dead zones, cut/reset buttons and both camera-switch bindings. Physics frames advanced during the run.

The first geometry application exposed a static-initialization-order error: the allocation was built before the direction array existed. I moved allocation initialization after its inputs, recompiled and reran the full checks. I did not change the pass criteria. The motion checks show about 29–30 mm of vertical displacement under full horizontal thrust over 0.8 seconds, because the measured thrust lines sit below the provisional center of mass. This is retained and documented rather than removed by moving the force points.

Unity detects an Xbox Controller. Automated mapping tests use temporary virtual devices; they do not prove physical stick/button operation. I confirmed that all controls work in a hands-on test with that controller. No hardware is commanded by these checks.

The updated Windows x64 build succeeded with **zero errors and zero warnings**, producing 112,701,192 bytes of build output. An earlier build warned about pending Editor code changes; after Unity reported scripts up to date, I rebuilt successfully without that warning. The standalone executable has not been separately launch-tested.

## Remaining limits

Mass, displacement, drag, inertia and thrust curves are provisional; thrust lines now come from CAD. Passing these checks establishes the prototype's internal behavior, not agreement with the real ROV. Long-duration operation and frame-rate performance remain to be measured. Controller and camera coverage for the later revision is recorded above.

There is no ROS, SITL, MAVLink, tether or real-hardware connection in this prototype. The simulation cannot command the ESP32 or ARK.
