# Ethernet tether training model

Default cable length is **100 ft / 30.48 m**, with **neutral net weight (scale 0)** so the balanced ROV does not sink under a slack tether. This is a training flotation assumption, not a claim that bare Ethernet cable is neutrally buoyant. Drag and the length limit remain active. Saved presets with an explicit nonzero weight retain it; set COURSE → TRAINING CONDITIONS & SCENARIOS → Net weight to 0 to make those neutral. Controls are under **COURSE → TRAINING CONDITIONS & SCENARIOS**: Ethernet tether on/off, cable length (3–60 m), drag scale (0–3), and net submerged weight scale (0–3). These values are included in custom scenario saves and comparison keys. Changing them during a run makes it practice only, using the existing qualification rules. Earlier scenario files receive the new defaults; earlier score records remain preserved, with different keys from the new tether-equipped conditions.

The status strip and COURSE panel show slack/near-limit/taut state, deployed/available cable and total applied load. The orange cable is a visual aid. Its bank endpoint is fixed at the pool rim nearest the **saved launch position**, including custom starts. Vehicle/map start reset relocates the endpoint and clears payout/load. It does not follow the pilot as the vehicle moves or change banks on code reload. All six maps use their existing floor bounds to select the bank.

## Reference and assumptions

An illustrative Cat6 cable, [Belden 2412](https://www.belden.com/products/cable/ethernet-cable/category-6-cable/2412), specifies nominal 5.69 mm outside diameter and bulk mass 33 kg/km (33 g/m). These seed the model; they do not identify the user's actual cable. The user has specified only ordinary Cat5/Cat6 and approximately 100 ft. The drag coefficient, load sharing, spring/damping and visual curve remain provisional simulation choices, not measured cable behaviour.

- Automatic payout starts at straight endpoint distance + 2 m slack, capped by available length. It increases as needed and does not reel in when the ROV returns.
- Spring tension begins only when straight endpoint distance reaches the total available cable length: `max(0, spring * extension + damping * outward endpoint speed)`, capped at 80 N. Defaults are 80 N/m and 12 N·s/m. This is a soft length limit; it permits stretch and does not teleport or hard-clamp vehicle position. Caps are numerical training safeguards, not cable/connector ratings.
- Transverse distributed drag uses `0.5 * water density * Cd * diameter * wet length * transverse speed * transverse velocity`. The model uses half endpoint velocity for average cable motion and shares half the resultant load with the vehicle. Default Cd is an adjustable Inspector coefficient of 1.2. Axial friction is omitted; transverse drag is capped at 40 N.
- Wet length is estimated with 12 samples along the straight anchor–vehicle segment. Half the net submerged weight (`linear mass minus displaced water mass`) is applied vertically, scaled by the UI weight setting and capped to ±10 N. A weight scale of zero removes this term. Airborne segments add no submerged weight/drag. Cable-floor support is not solved.
- Force acts at a provisional rear/upper attachment `(0, 0.075, -0.14)` m in vehicle-local coordinates, so it can create torque as well as translation.

## Integration and scope

`RovDynamics.ApplyForces` calls `RovTether.ApplyForces` exactly once per physics step, both for local FixedUpdate ownership and externally stepped SITL physics. The tether has no separate FixedUpdate. Current, water density and rigidbody point velocity come from the existing dynamics. Sensor outputs respond through the resulting vehicle motion; no ROS, DVL validity, ground-truth forwarding, firmware, hardware or watchdog code changes.

Rendering is a 33-point LineRenderer refreshed at up to 20 Hz, with an approximate slack curve/current bow clipped visually above the pool floor. The cable curve is not length-constrained or a catenary solver. No collider, rigidbody, contact, snag or wrapping is added to the cable, and it does not interfere with DVL raycasts. The visual width is 1.5× nominal diameter for legibility. A resource material is referenced so it is included in builds; the runtime instance is released with the component.

## Validation

`ClubROV.Simulation.Editor.RovTetherChecks.Run()` passes 35 checks covering length conversion, unilateral/capped tension, transverse motion/current drag, deployed-length scaling, net weight and buoyancy, dry segments, scenario JSON/legacy defaults/key separation, finite setting rejection, actual rigidbody pull, local/external step parity, a five-second 400 Hz spring stability test, 30-second zero-thrust neutral-depth tests at 50 and 400 Hz (less than 5 mm drift, zero current), disabling, launch-reset anchoring, visual lifecycle and all six map footprints. Rigidbody integration runs in a separate LocalPhysicsMode.Physics3D scene. The user's selected map and vehicle pose are checked unchanged.

The 26 run-review and 27 course-guidance checks also pass. Unity compiles without errors or warnings. The cable and status strip were inspected in the live Game view; the original camera mode was restored afterward. A full SITL/QGC session, autonomous mission and physical cable calibration were not run for this change.
