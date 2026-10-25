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
- Camera streaming into QGC, ROS integration, realistic tether/sonar/wave models and physical ROV control are not implemented or validated here.
- No calibrated optics, comprehensive GPU compatibility study or long-duration performance qualification is claimed.

## Reproducing checks

The check implementations remain under [Assets/ROV/Editor](../Assets/ROV/Editor/). They require a running Unity Editor with the training scene and the relevant Play Mode state; synthetic input checks require native application focus. Review each check's setup and cleanup behavior before running it. SITL setup and the guarded MAVLink check are in [Simulation/SITL](../../SITL/README.md).

Generated reports and old diagnostic screenshots are local development outputs, excluded from Git. The detailed records from this validation were retained in the private maintainer archive; the public repository keeps the implementations, this summary and the current README preview.
