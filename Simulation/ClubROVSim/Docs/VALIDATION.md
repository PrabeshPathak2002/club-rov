# Simulator validation

Recorded validation: **23–24 October 2026**, Unity **6000.6.7f1**, Windows x64, ArduSub **4.7.2**. This summary describes recorded development checks, not tests rerun whenever the project opens. Current controls and setup are in the [simulator guide](../README.md).

## Recorded checks

- **Vehicle model:** six mixer checks, eleven bounded movement/lifecycle checks and twelve synthetic input/camera checks covered direction, neutral, force limiting, reset, floor contact and ownership cleanup. CAD thrust axes and the envelope drive the model; mass, drag, displacement, inertia and thrust coefficients remain provisional.
- **Course and visibility:** nineteen checks covered gate ordering/direction/bounds, hover/docking requirements, collision counting, completion timing, reset and underwater/above-water visibility behavior.
- **FPV:** ten rendering/lifecycle checks covered sampled video cadence, lens/aspect, native-resolution HUD, shader setup, camera switching and buffer cleanup. Camera softness, particles, spotlight and delay are training approximations.
- **Scenarios and status:** twenty-four training-upgrade checks covered disturbances, scenario persistence/error handling, run scoring and actual simulator-heartbeat arming status.
- **Maps and artwork:** eighteen map checks, twenty-six handbook-layout checks and seventy-two artwork checks covered map selection, gate/pillar logic, arena bounds, station counts, material assignments and torpedo-board openings.
- **Views and instruments:** fifteen view/panel checks and twenty-four instrument-style checks covered camera cycling, panel visibility, attitude/heading signs, style selection, preferences and lifecycle behavior.
- **SITL:** twelve protocol checks and twelve link/ownership/timeout checks passed, followed by live actuator/sensor exchange and arming/disarming checks. Hands-on QGC tests confirmed manual control, depth hold and heading hold. See the [SITL validation summary](../../SITL/README.md#validation).

A physical Xbox controller was used to confirm the initial local bindings. Later shortcut/style checks used synthetic inputs; that is separate evidence from a physical-controller test of every later feature.

## Build and visual checks

Windows x64 builds were completed during these revisions. The final instrument-style build recorded zero build errors and warnings. Earlier builds had revision-specific warnings documented in the retained development records.

Editor captures were inspected for the course, visibility, sampled FPV, handbook layouts, top-down view and instrument styles. Standalone startup and selected scenario/camera actions were also checked during development. These observations do not establish a fully piloted run on every map or minimum hardware requirements.

## Remaining limits

- Physical vehicle calibration, absolute depth calibration and autopilot tuning remain incomplete.
- No full scored course under every disturbance preset has been validated end to end.
- Competition stations are static training geometry, not complete task interactions or official judging.
- Camera streaming into QGC, physical ROS/ArduSub navigation feedback, realistic tether/sonar/wave models and physical ROV control remain unvalidated here. Simulated ROS sensor integration is described below.
- No calibrated optics, comprehensive GPU compatibility study or long-duration performance qualification is claimed.

## Reproducing checks

The check implementations remain under [Assets/ROV/Editor](../Assets/ROV/Editor/). They require a running Unity Editor with the training scene and the relevant Play Mode state; synthetic input checks require native application focus. Review each check's setup and cleanup behavior before running it. SITL setup and the guarded MAVLink check are in [Simulation/SITL](../../SITL/README.md).

Generated reports and old diagnostic screenshots are local development outputs, excluded from Git. The detailed records from this validation were retained in the private maintainer archive; the public repository keeps the implementations, this summary and the current README preview.

## Local roll control - 26 October 2026

Unity compiled the roll input/mixer change successfully. The existing six mixer checks and 17 synthetic input/camera checks passed. Dedicated roll checks confirmed opposite-sign torque without net translation, bounded combined commands, thrust cut, and rejection of local commands under SITL ownership.

Four isolated 30-second PhysX runs used the provisional 8 kg mass, 15 N forward thrust, 0.8 reverse ratio, 35 mm buoyancy offset and existing damping. At 60% thrust, both directions settled near 52.8 degrees of roll. At 100%, both directions completed about 1547 degrees of accumulated body-axis rotation and reached an inverted attitude. These isolated runs exclude the pool, tether and physical hardware. No live QGC roll maneuver or physical controller roll trial is claimed by these checks.

## Simulated ROS 2 navigation sensors

The Humble packages built on Ubuntu 22.04 and passed 15 protocol/transport tests. Fifteen isolated Unity sensor checks passed. A live Unity-to-WSL run published DVL, raw IMU and depth measurements into a Madgwick attitude filter and robot_localization EKF, yielding finite local odometry with ground truth disabled. The stationary eight-second observation ended approximately 5 mm forward, 19 mm left and 6 mm below its launch origin. These values describe one integration test, not a calibrated accuracy result.

The stream supports local and externally stepped physics through RovDynamics.PhysicsStepped. A subsequent Ubuntu 22.04 migration check ran ROS and ArduSub SITL together: the sensor/local-odometry live check passed while ArduSub owned physics, and the guarded simulator motor check passed and ended disarmed. That migration check preceded the optional external-navigation validation below; real DVL operation remains unvalidated. See [the ROS guide](../../../ROS2/README.md) for topics, frame conventions, reset behavior and measurement-model limits.

The DVL exterior, adapter and nominal hardware were subsequently tessellated from the Fusion assembly and installed as four renderer groups totalling 16,340 triangles. Geometry checks verified the 85 mm housing width, housing mounting height and 180 x 130 x 5 mm adapter envelope. An underside Scene view was inspected. The visual contains no additional rigid bodies or colliders; physical mass and contact behavior remain provisional.

## DVL-assisted external navigation (30 October 2026)

The pinned Sub-4.7.2 MAVLink odometry backend was verified from source (`VISO_TYPE=1`). The optional ROS bridge converts launch-relative FLU odometry to LOCAL_FRD/BODY_FRD, verifies the isolated SYSID42 simulator and source settings, and initializes its synthetic EKF origin. Horizontal position/velocity use ExternalNav; compass yaw and pressure depth remain ArduSub inputs. ROS never consumes ArduSub fused pose or simulator position truth.

Eighteen Humble Python tests passed, including heading/body-axis conversion and nonfinite/uncertain pose rejection. Live QGC-compatible TCP telemetry contained global/local position with EKF horizontal absolute position valid. Position Hold was accepted and armed in the guarded simulator test. Under a 0.2 m/s simulated cross-current, eight simulation seconds of neutral hold had 0.012 m maximum horizontal excursion. Pilot reposition traveled 0.247 m, then settled to 0.005 m/s horizontal speed. The test ended disarmed. The report remains outside Git under `~/club-rov-sitl/run-external-nav/external-nav-check.json`.

Injected 100% DVL dropout withheld odometry with a constant sent-message counter across three consecutive diagnostics. This checks sender gating, not a complete autonomous recovery policy or calibrated navigation accuracy. A combined launcher start/stop verified the ROS and relay lifecycle. Standard and navigation profiles retain separate EEPROM directories. Mass, noise, covariance, heading drift and physical hardware still require calibration.

## Consolidated HUD sidebar (30 October 2026)

The Controls, Course and Vehicle panels now open from a compact left sidebar into one scrollable drawer. All drawers start closed. A slim status strip retains arm state and basic telemetry; H hides it with the dock, MENU restores it, and Close/Esc closes the active drawer. Existing control names, callbacks, training foldouts and independent FPV instruments are retained.

Twenty-three view/sidebar checks passed, covering camera modes, exclusive drawer selection, close/toggle behavior, hide/restore and component re-enable without duplicate controls. Chase, Controls drawer and FPV captures were visually checked. Unity compiled without errors. This update was validated in the Editor; a standalone player must be rebuilt to include it.

## Desktop SITL timing check (30 October 2026)

With the consolidated sidebar visible, a five-second sample measured 58.2 FPS and a 0.70 simulation/wall-time ratio; hiding the HUD measured 60.0 FPS and 0.73. The first sample included a 187 ms frame spike. No artificial FPV delay was configured. The active simulator scheduler was changed to 200 Hz and the full stack restarted. A subsequent eight-second sample measured 60.0 FPS, 20.6 ms p95 frame time and a 1.00 simulation/wall-time ratio. The JSON physics packet rate remained 400 Hz. A follow-up depth-hold check had 0.017 m steady depth variation and ended disarmed. Restart and scheduler effects were not isolated from each other; longer-session profiling remains useful.

## ROS DVL track minimap (30 October 2026)

A top-right map now consumes ROS `/odometry/local` returned through the existing sensor TCP connection and Windows loopback UDP9012. No Unity vehicle transform, ArduSub fused pose or ground-truth topic supplies map coordinates. Estimated yaw controls the arrow in a launch-forward-up frame. Trails retain up to512 movement samples, draw at10Hz, break across invalid/stale intervals and reset on new sensor sessions.

Sixteen model checks passed for coordinate/heading signs, validity, paused clocks, gaps, replay/session rejection, mission resets, bounded history and drawing-only clear. Eighteen Humble tests passed, including TCP return framing. The23 sidebar/view checks passed with the minimap installed, including HUD disable/re-enable socket cleanup. A live simulated maneuver produced124 trail points at estimated position(8.68,-2.65)m with yaw-1.47rad. Injected DVL loss froze144 points and the last position while showing a paused gray arrow. Extended loss triggered the existing estimator uncertainty guard; a full session restart is required once uncertainty grows too large.

A post-test sample measured59.9FPS; SITL advanced at0.75x wall time in that sample. Rendering remained near the60FPS cap, but this feature does not resolve the existing variable SITL pacing or long-outage estimator limitations. The maneuver ended disarmed. Unity compiled without errors; standalone builds need rebuilding.

## End-to-end simulator validation (31 October 2026)

See [the dated validation report](END_TO_END_VALIDATION_2026-10-31.md): 168 functional Unity checks and 26 ROS tests passed; one legacy synthetic HUD check remains recorded as failed. Native menu/review/restart flow passed after recovering from a reproducible dynamic-evaluation input failure. Fresh autonomous mission completed with 0.108 m Unity return error; total DVL loss triggered surface/disarm. These were isolated simulator trials, not hardware or repeatability qualification.

## Publication verification - 21 November 2026

The source publication includes the simulator extensions, ROS/SITL navigation, prequalification/calibration tools and course-learning workflows. ROS 2 Humble rebuilt both packages and passed 82 package tests. The learning tools passed 35 Python tests. Supplied model, map and inference-runtime hashes match the recorded manifest. Unity asset metadata has no duplicate GUIDs or missing asset meta files. PowerShell syntax, 26 shell scripts and repeatable local Inference Engine package preparation were checked. Firmware host verification passed 110,979 frame-delivery cases and 168 heartbeat cases.

A clean source-snapshot Unity batch import stopped before compilation with exit code 198 because the host had no active Unity Editor license. Consequently this publication review does not establish fresh Unity compilation, player-build success or live runtime behavior. The dated successful build and runtime observations above remain historical evidence. Activate Unity 6000.6.7f1, resolve packages and run Tools/Prepare-InferencePackage.ps1 with Unity closed before repeating a Windows player build.
