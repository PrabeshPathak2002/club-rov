# Club ROV simulator

I am building this Unity prototype as a pool-training and sensor-experiment environment for the club ROV. It uses the existing assembly viewing mesh, a six-thruster force model and a 20 × 24 m training pool, 3.5 m deep.

Editor: **Unity 6000.6.7f1**. Render pipeline: **URP 17.6.0**. Input: **Unity Input System 1.20.1**. The approved Unity Pipeline package provides local Editor automation; no cloud project is required.

## Run it

1. Add this folder to Unity Hub as an existing project and open it with the matching Editor.
2. Open `Assets/Scenes/PoolTraining.unity`.
3. Press Play, then click the Game view to give it keyboard/controller focus.

The validated local Windows build is in `Builds/Windows/ClubROVSim.exe`. Keep the entire Windows folder together when copying it. Build output is excluded from Git.

Keyboard controls:

- **W/S:** forward/backward.
- **A/D:** move sideways.
- **R/F:** ascend/descend.
- **Q/E:** turn left/right.
- **V:** switch between chase and onboard camera views.
- **Home:** reset position, orientation, velocity and thruster commands.
- **Space:** cut simulated thrust. The vehicle still has inertia.

Gamepad controls: left stick moves horizontally; right stick controls yaw and vertical thrust; Start resets; Back/Select switches camera; A/south button cuts thrust. Synthetic keyboard/gamepad checks verify these mappings, and I confirmed all controls with the connected Xbox Controller. Losing application focus clears pilot thrust commands.

For tuning, select **Club ROV - simulation** in the Hierarchy and change the **Rov Pilot Input** component in the Inspector before pressing Play. Defaults are **0.15 stick dead zone**, **0.65 translation sensitivity**, **0.50 yaw sensitivity** and **0.60 maximum thrust**. Sensitivity scales the requested motion; the thrust limit caps all mixed actuator forces together to preserve balance. Values changed during Play are temporary. The keyboard uses the same sensitivity and thrust limit, with no stick dead zone.

![Pool training course](Assets/Screenshots/training-course.png)

## FPV camera feed

**V** or controller **Back/Select** switches between the clean chase display and a Pi-camera-style FPV feed. FPV uses **640 × 480**, **4:3 framing** and a **30 fps maximum sample cadence**, with mild softness, darker-area sensor grain, 8-bit color steps and subtle block variation. The HUD stays at native display resolution. Only the camera video is affected; vehicle physics and input continue at their normal rates.

The starter lens uses **48.8° vertical field of view**, based on the standard [Raspberry Pi Camera Module 2 specification](https://www.raspberrypi.com/documentation/accessories/camera.html). This is an appearance preset, not a claim about the real camera's maximum resolution, configured stream, underwater housing optics or measured noise. FPV follows the vehicle's full orientation, including roll and pitch. Camera placement remains the existing forward viewing offset rather than an optical model through the enclosure window.

Select **Main Camera → Rov Fpv Camera Feed** before Play to change resolution, frame rate, lens field of view, noise or softness. Switching back to chase restores its lens and viewport and releases the held video frame. There is no simulated network latency, packet loss or real codec yet; block variation is only an approximation.

![Pi-camera-style FPV](Assets/Screenshots/fpv-camera-feed.png)

## Training course and visibility

Cross the three yellow gates in order, toward the far end of the pool (+Z). The cyan pips above each gate identify its number. Crossing backward or outside the inset opening does not score. The HUD gives the next target's distance, bearing and depth.

After gate 3, hold inside the **cyan hover target** for **3 continuous seconds**, heading **000° ±15°**, at less than **0.15 m/s**. Leaving the 0.45 m target radius, turning too far or moving too fast resets the hold. Then enter the **green docking bay**, heading **180° ±15°**, and remain within 0.40 m horizontally and 0.18 m vertically of its target at less than **0.10 m/s** for **3 seconds**.

The timer starts with movement and freezes on completion. The collision counter records contact entries, with a 0.4 s debounce to avoid counting several contacts from one impact. Contact does not invalidate the run. **Restart Course**, **Home** or controller **Start** resets the vehicle, timer, objectives and collisions. **Space/A** cuts thrust without resetting the course.

Use the HUD's **visibility slider** from **1–20 m**; the default is **8 m**. It controls exponential underwater fog and water tint, with scene contrast reduced to roughly 2% at the selected distance. This is a visual training approximation, not a measured turbidity model. Fog disables above the water surface; no wave, refraction or scattering solver is implemented. The slider and restart button accept mouse input without taking the controller's piloting bindings.

![Murky-water training at 2 m visibility](Assets/Screenshots/training-murky.png)

## What the model does

Four angled horizontal thrusters supply surge, sway and yaw. Two vertical thrusters supply heave. Each command becomes a force at an individual body-space position, with a simple response delay and reduced reverse thrust. The rigid body also receives gravity, buoyancy, quadratic linear drag and angular damping. An offset center of buoyancy gives passive roll/pitch stability.

The HUD shows simulated depth, speed, heading and all six normalized thruster-force commands. The horizontal mixer allocates sway, surge and yaw using the measured lever arms and accounts for the weaker reverse limit before applying force. These are force fractions, not PWM/DShot values. H1 is front port, H2 front starboard, H3 rear port, H4 rear starboard, V1 port and V2 starboard. These labels and positive axis signs are local simulation conventions, not a verified ArduSub channel mapping.

Units are metres, kilograms, seconds and newtons. Unity axes are +Z forward, +X starboard and +Y up. The mesh importer converts the source CAD millimetres and centers the visual mesh. The imported visual bounds are approximately 0.38 × 0.26 × 0.36 m. A box collider approximates the hull; it does not reproduce every rail or thruster guard.

The current live Fusion dimensions, six thrust-axis locations and mounting angles are recorded in [Fusion geometry](Docs/FUSION_GEOMETRY.md). They now drive the force locations and envelope collider. My printed parts use Bambu Lab PETG Basic at 100% infill.

## Provisional parameters

I still need measurements from the real ROV. The initial values are **8 kg mass**, **0.008 m³ displacement**, **1000 kg/m³ water density**, **15 N full forward thrust per motor**, **0.8 reverse ratio**, **0.15 s response** and **35 mm buoyancy offset**. Angular speed is capped at **1.2 rad/s** for the starter training setup. These are starting values, not measured U01 performance or a verified vehicle mass. Thrust lines and the envelope now come from CAD; mass, drag, inertia, buoyancy and actual motor direction still need calibration. Forces use the geometric center as a provisional center of mass, so horizontal thrust can produce pitch/roll moments until the actual balance is measured.

The initial displacement makes the 8 kg rigid body neutrally buoyant when fully submerged. An approximate submerged fraction reduces buoyancy and thrust near the flat water surface. There is no tether model, added mass, wave model, realistic sonar, sensor-noise model or photorealistic underwater optics yet.

## Source layout

- `Assets/ROV/Scripts/RovDynamics.cs`: buoyancy, drag and six actuator forces.
- `RovThrusterLayout.cs`: provisional force locations/directions and normalized mixer.
- `RovPilotInput.cs`: keyboard/gamepad input, separated from physics for later integration.
- `RovCameraRig.cs`: chase/onboard camera selection.
- `RovTrainingCourse.cs`: ordered gate crossing, hover/docking holds, run timer and collision count.
- `RovWaterVisibility.cs`: per-camera underwater fog/tint and visibility control.
- `RovFpvCameraFeed.cs`, `RovFpvRendererFeature.cs` and `RovFpvFeed.shader`: FPV profile and URP Render Graph video sampling/display before the HUD.
- `Assets/ROV/UI/`: UI Toolkit HUD and display logic.
- `Assets/ROV/Editor/PoolSceneBuilder.cs`: repeatable scene creation, STL conversion and mixer checks. It refuses to overwrite an existing PoolTraining scene.
- `RovSmokeChecks.cs`: bounded Play Mode checks that restore the vehicle and input afterward.
- `TrainingPoolBuilder.cs`: one-time Editor upgrade to the larger pool and course; refuses duplicate installation.
- `RovCourseChecks.cs`: course rules, HUD pointer reset, visibility and pool-size checks.
- `FpvFeedSetup.cs` and `RovFpvChecks.cs`: Editor installation and bounded FPV cadence, lens and buffer-lifecycle checks.
- `Docs/AI/UnityProjectContext.md`: project conventions and tooling context.

The generated mesh and scene are included, so opening this project does not require a CAD importer. Rebuilding the visual mesh uses `../../Models/club-rov-preview.stl` from the parent ROV repository. The source assembly predates the newest mechanical changes; commercial reference geometry keeps its existing [attribution](../../Documentation/PUBLISHING.md).

## Validation and next steps

The training revision passed six mixer checks, eleven Play Mode motion/lifecycle checks, twelve synthetic keyboard/gamepad/camera checks and nineteen course/UI/visibility checks. See [validation notes](Docs/VALIDATION.md) for evidence and remaining limits.

This prototype is isolated from the physical ROV. I can now use ArduSub 4.7.2 SITL as the controller, with Unity supplying physics and sensor data through its JSON backend. See the [SITL setup and validation](../SITL/README.md) for the launcher, QGroundControl connection and remaining limits. Local piloting remains the default; CONNECT SITL explicitly transfers command ownership. ROS 2 and real-hardware connections are not implemented.
