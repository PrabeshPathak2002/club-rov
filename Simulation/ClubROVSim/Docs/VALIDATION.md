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

## Larger pool, course and visibility revision

The saved scene now has a 20 × 24 m pool with the existing 3.5 m depth, three numbered gates, a cyan hover target and a green dock. I verified the HUD at 1600 × 900 and inspected captured 8 m and 2 m visibility views. Gate visibility and scene contrast drop visibly in the murky view while the HUD remains readable.

All **19 course/UI/visibility checks** passed: out-of-order, backward and outside-opening rejection; ordered progress; hover heading/speed/continuous-hold requirements; docking requirements; collision debounce; frozen finish statistics; the live visibility slider; above-water fog disable; pointer-operated HUD reset; and expanded pool dimensions. See [course results](course-checks.json). These exercise course logic using injected poses; they do not claim a fully piloted course run.

The initial reverse-crossing test had crossed forward while positioning the vehicle. I changed its starting pose to the far side before resetting tracking, preserving the rejection criterion. The initial synthetic navigation-submit event did not exercise the mouse restart button; pointer-down/up events now exercise its click behavior. Reset tracking now reads the rigid-body pose to avoid interpolation lag. The **11 physics checks** and **12 input/camera checks** passed again; the floor-contact check additionally verifies an actual collision increments the course counter. Six measured-layout mixer checks remain applicable.

URP camera rendering applies the fog; camera-above-water behavior and settings restoration are implemented. Fog distance is an artistic contrast threshold, not measured underwater optical performance. The controller confirmation from the preceding revision remains the physical-device evidence; the training course has not been manually completed end to end.

The saved training scene built successfully for Windows x64 with **zero errors and zero warnings**; output size was 112,744,172 bytes. Unity's modified-scene save dialog was resolved before building. The executable is `Builds/Windows/ClubROVSim.exe`; the output folder must be kept together. Standalone launch behavior has not been separately tested.

## Pi-camera-style FPV revision

I inspected FPV and chase captures at 1600 × 900. The scene image in FPV is sampled to 640 × 480 and letterboxed, while the HUD stays sharp. I reduced the initial grain after viewing it; the saved sensor-noise value is 0.0025, with softness 0.45. The 48.8° vertical lens baseline is from the standard Pi Camera Module 2; the preset does not reproduce a measured stream or underwater optical system.

All **10 bounded FPV checks** passed: clean chase bypass, video-history allocation, capped sampling cadence, lens/aspect, native HUD overlay, shader compilation, chase projection restoration, history release, reallocation on return and cleanup when disabled. The timing observation was **29 video samples over 1.000 seconds with 351 Unity frames**. See [FPV results](fpv-checks.json). The **12 synthetic controls/camera checks** and **19 course/UI/visibility checks** also passed with the renderer installed. A naming collision in the Editor-only check class was corrected before the passing run.

The Render Graph review verified an explicit source texture through BlitMaterialParameters, separate sampled history and display targets, an intermediate-color requirement, a persistent external handle for cross-frame history, and cleanup of the handle/material. The intended scope is the single ROV game camera. No measured end-to-end latency, codec fidelity or video-noise calibration is claimed.

The FPV revision built successfully for Windows x64 with **zero errors and zero warnings**, at 112,759,664 bytes. An initial shader warning about fractional power on an unconstrained value was resolved by clamping the vignette input and using square roots; the final build is clean. The standalone executable has not been separately launch-tested.

## Remaining limits

Mass, displacement, drag, inertia and thrust curves are provisional; thrust lines now come from CAD. Passing these checks establishes the prototype's internal behavior, not agreement with the real ROV. Long-duration operation and frame-rate performance remain to be measured. Controller and camera coverage for the later revision is recorded above.

There is no ROS, tether or real-hardware connection in this prototype. The simulation cannot command the ESP32 or ARK. SITL and MAVLink support were added in the revision below.

## SITL integration validation

ArduSub 4.7.2 built in WSL2 from pinned revision fb773567896014da007bca58358704d4a0d8516f. Live JSON exchange settled to 400 Hz. The final configuration uses MAV_SYSID=42 and FRAME_CONFIG=1 (six-motor VECTORED). The guarded MAVLink test verified neutral, forward, starboard, right-yaw and upward outputs, then disarmed with all six channels at 1500 µs. Unity visibly translated, turned and rose. QGroundControl received live telemetry over the dedicated localhost TCP 5765 link. I subsequently configured the controller and confirmed manual control, depth hold and heading hold in hands-on tests.

Twelve protocol and twelve UDP/ownership/duplicate/timeout checks passed. Regression checks passed: eleven local motion/lifecycle, twelve synthetic controls/camera (with native Unity focus), nineteen course/UI/visibility and ten FPV checks. The Windows build succeeded with zero build errors and warnings, 112,769,468 bytes. Existing shader warnings in the Editor were not introduced by SITL. Absolute depth calibration remains untested. See [SITL notes](../../SITL/README.md) and its recorded MAVLink report.

## Training upgrades — 24 October 2026

I added bounded current/buoyancy/thruster disturbances, repeatable built-in and custom scenarios, scored completed runs with condition-specific personal bests, suspended particles, a camera spotlight and 0–500 ms FPV delay. The arming badge reads actual system-42 heartbeats over the relay rather than inferring arming from PWM.

All 24 focused [training-upgrade checks](TrainingUpgradeCheckResults.txt) passed, including force effects, scenario round-trip/backup/corruption handling, personal-best retention, GUI-to-physics binding, delayed-buffer warmup/release, particle/light gating, wrong-system rejection and stale-status handling. The live ArduSub test observed both ARMED and DISARMED heartbeats in Unity and finished disarmed. Local motion (11), synthetic controls/camera (12), course/UI/visibility (19), FPV (10), SITL protocol (12) and link lifecycle (12) regressions passed. The final Windows build succeeded with zero errors and zero warnings, 112,839,528 bytes. The expanded panel and murky FPV feed were visually inspected.

I also launch-tested the final standalone Windows player at 1280 × 800: opening the training panel, switching Chase/FPV with the mouse button, rendering suspended particles and applying the murky-water preset worked. No script exceptions or custom-FPV shader failures appeared. The player logged stripped/unsupported shader diagnostics for unused URP Gaussian/Bokeh depth-of-field and Panini-projection passes; the tested camera effects rendered correctly.

The optical effects and disturbance ranges are training approximations. I have not completed a fully piloted scored course under every preset, tested the new effects on another GPU, or matched dynamics to the real vehicle. Hands-on depth/heading hold confirmation predates these new disturbances; each disturbed scenario should be practiced separately.


Training maps (24 October 2026): 18 compiled `RovMapChecks.Run` checks and seven direct runtime checks passed; see `MapCheckResults.txt`. Checked four layout selections, repeatable start, gate bounds/outbound/return sequence, orbit completion in either direction, premature return rejection, large position jumps, free-swim objective suppression, scenario copy/key identity, map selector/load action, gate dimensions, surface score invalidation and ExternalPhysics map-switch guard. A final check confirmed conditions presets retain the selected map. Original 19 course/UI/pool/visibility checks passed again. Synthetic completion runs disabled result recording. Windows x64 player build succeeded with zero build errors/warnings. Gate/pillar geometry was inspected in Game view. No complete hand-piloted/autonomous run or new live SITL session on each map was performed. The final loaded Editor assemblies passed the map and course checks again after refreshing outside the automation scope. Final Console: zero errors and warnings.


RoboSub 2026 handbook maps (24 October 2026): 26 `RoboSub2026Setup.Check()` runtime checks passed against the saved practice/finals arenas, followed by the 18 existing map checks and 19 original course/UI checks. Verified station counts, three-row slaloms, path counts, 2.1 m floor, original/new environment exclusion and restoration, shallow-pool scenario bounds, map-aware presets, and the ExternalPhysics switch guard. Top-down practice/finals captures were compared with handbook pp. 80/82. The map-load UI action and normal FPV camera were exercised in a fresh Play session; screenshots are in Assets/Screenshots/robosub-2026-*.png. No new live SITL piloting session, autonomous mission, physical course survey, official judging or task interaction validation was performed. The static role markings and unspecified component dimensions remain documented approximations. Temporary overview-camera/HUD changes were discarded on exiting Play Mode.

The handbook-map Windows x64 player build succeeded with zero build errors and warnings. It was not separately launch-tested; the new layouts and FPV were exercised in the Editor.

## Official task artwork — 24 October 2026

I replaced the placeholder role markings in both RoboSub 2026 maps with 16 unmodified official task images, covering 114 textured surfaces on gates, bins, torpedo boards, octagon signs, baskets and samples. The torpedo-board cutouts follow the official SVG geometry.

All 72 artwork checks passed, including material assignments and raycasts through every board opening and against its rim. The 26 handbook-layout, 18 training-map and 19 course/UI/visibility checks also passed. I visually checked the official gate signs in the normal FPV view; other task artwork has automated assignment and geometry checks but no separate visual sign-off. All 16 PNG hashes match the provenance manifest.

The Windows x64 build succeeded with zero errors and one warning: Unity automatically pre-baked collision for the two generated board meshes and recommends explicitly enabling pre-baked collision for future Unity versions. The final Editor Console had zero errors and that same warning. The player was not separately launch-tested. No new live SITL or task-interaction test was performed; these maps remain unscored course exploration.

## Graphics and portable practice — 24 October 2026

Low/Medium/High were exercised through the actual HUD callbacks: render scales 0.65/0.85/1, shadow distances 0/15/30 m and HDR only on High. All three FPS caps set Application.targetFrameRate correctly. Medium/60 displayed approximately 60 FPS and 16.7 ms/frame in the Editor; this is not a hardware benchmark. The HUD was visually inspected at 1600×900. Nineteen course/UI checks and ten asynchronous FPV rendering/resource checks passed. Runtime pipeline changes use a clone and restore the original pipeline and frame settings on HUD disable. The Windows practice package includes a quickstart and optional 1280×720 launcher. Other-computer testing remains pending.

The first build exposed a URP build-cache reference to a destroyed Play Mode pipeline clone. An Editor-only cleanup hook now disposes CoreBuildData on return to Edit Mode. A rebuild after clearing that cache succeeded with zero errors and zero warnings. The standalone player initialized Direct3D 12, input and the scene without script exceptions in its captured log; this was a startup smoke check, not a complete player course run.

A fresh Play session confirmed saved Medium/60 values, restoration of the original pipeline on HUD disable and successful re-enable. A subsequent build without manual cache cleanup succeeded with zero errors/warnings, validating the Editor hook. The practice ZIP contains 190 entries and the executable, UnityPlayer.dll, quickstart and launcher, with no Unity backup folder.
