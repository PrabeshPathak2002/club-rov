# Club ROV: A Modular Six-Thruster Training Platform

Technical Design Report | Revision 0.1 - Integration draft | 26 October 2026

[Formatted PDF](output/pdf/Club_ROV_TDR.pdf). Rebuild with `python build_report.py` from this directory; Python packages `reportlab` and `Pillow` are required. Figure directives below are rendered into the PDF from existing project assets and original vector diagrams.

Club ROV project. Evidence baseline: repository revision e9bd47d; hardware records dated 20-23 October 2026 and simulator records dated 23-24 October 2026. No new hardware test is implied by the report date.

<!-- page: Mission and requirements -->

## Abstract

Club ROV is a tethered six-thruster underwater platform for student training, demonstrations and progressive sensor integration. An ARK FPV controller running ArduSub provides vehicle control, while an ESP32-P4 companion transports MAVLink and hardware-encoded camera video over Ethernet. A modular printed frame, removable electrical connections and an ESC-above-controller adapter support servicing in a compact enclosure. A Unity simulator uses CAD-derived thrust-axis geometry and connects to ArduSub software-in-the-loop (SITL), allowing controller practice before physical integration is complete. Recorded bench work establishes communications, video reception and a guarded firmware-update path; recorded simulator work establishes internal motion, course and control-interface behavior. Physical motor mapping, assembled mass, buoyancy, endurance, sealing and depth qualification remain open. This report presents the current architecture, its design tradeoffs, the available verification evidence and the measurements required before an operating envelope can be assigned.

## I. Mission strategy

### A. Training before autonomy

The immediate mission is supervised tethered operation and an understandable path from mechanical assembly to embedded communications and piloting. The platform therefore prioritizes accessible interfaces and repeatable diagnosis over competition-specific manipulators. RoboSub-style gates, slalom and task layouts provide training contexts; autonomous completion of those tasks is not an implemented vehicle capability [3, 8].

The development sequence is communications bench validation, physical propulsion integration, shallow-water qualification, then additional sensing and autonomy. Simulator progress reduces the cost of learning controls and finding software defects, but it cannot close physical requirements by itself. The architecture separates operator control, transport and vehicle stabilization so each boundary can be checked independently.

<!-- figure: vehicle -->

### B. Requirements and evidence states

R1 requires four commanded motion axes: surge, sway, heave and yaw. R2 requires tethered command/telemetry and usable video at the surface. R3 requires service access without relying on loose, exposed power junctions. R4 requires explicit behavior when operator input or communications is lost. R5 requires containment and sealing qualified for the actual deployment. R6 requires repeatable software-only training with exclusive controller ownership.

These report-level design requirements are mapped to evidence and remaining qualification gates in Section VII. The previously discussed 20-50 m depth ambition is a future qualification target; the current vehicle has no demonstrated depth rating [3, 4].

### C. Scope and reporting approach

The report follows the objective-to-design-to-testing organization illustrated by the 2026 Ohio State and NUS reports [1, 2]. Their vehicle designs, results and graphics are not used as Club ROV evidence. This integration draft addresses one tethered ROV and its training simulator; measured cost, finished mass and endurance are left unreported until data exists.

<!-- page: System architecture -->

## II. System architecture

### A. Control and transport boundaries

The surface station runs QGroundControl (QGC). Operator messages travel through the Ethernet tether to the ESP32-P4, then through its USB host connection to the ARK FPV. The ARK runs ArduSub 4.7.2 and owns stabilization, mixing and actuator output. Four outputs serve the four-in-one ESC and two serve the separate Basic ESCs. Telemetry returns through the same USB/Ethernet transport [3, 5].

The companion does not arm the vehicle, synthesize an autopilot heartbeat or implement an independent emergency stop. Its responsibilities are forwarding, video and maintenance. This division avoids making the camera/communications application responsible for the flight-controller control loop. It also creates a clear dependency: a functioning video stream does not prove that commands, arming state or controller failsafes are correct.

<!-- figure: architecture -->

### B. Independent video and maintenance paths

The Raspberry Pi Camera V2.1 / IMX219 connects to the P4 CSI interface. The image passes through an ISP and hardware H.264 encoder, then RTP/UDP transport to QGC. Video and MAVLink use separate UDP sockets. A short bench observation recorded approximately 25 frames/s at 1632 x 1232 pixels with a configured 4 Mbit/s stream; latency was not measured [6].

The ESP32-C6 provides a maintenance hotspot. Its dashboard is also reachable over Ethernet. Maintenance Wi-Fi is an above-water service interface, not an underwater command or video link. Keeping it distinct from the tether prevents an informal assumption that Wi-Fi provides communications redundancy [3, 6].

### C. Defined interfaces

The configured Ethernet endpoints are 192.168.137.2/24 on the companion and 192.168.137.1/24 at the surface. MAVLink uses P4 UDP 14555 and surface UDP 14550; video uses surface UDP 5600. The USB path uses the ARK MAVLink CDC interface rather than its separate SLCAN interface. These values are configuration choices, not general requirements for every installation [3].

Endpoint filtering restricts accepted command datagrams to the configured source IP and port. It is not cryptographic authentication. A trusted local network and correctly configured controller failsafes remain necessary. ROS 2 routing and explicit manual/autonomous command arbitration are future work; adding a second MAVLink client does not itself resolve command ownership.

### D. Physical versus simulated vehicle

The physical ROV and the simulator use separate control paths. SITL is isolated at vehicle system ID 42 and local loopback endpoints. Unity supplies physics and sensor replies to a software ArduSub instance; it does not connect to the physical ARK. This separation supports repeatable training without presenting a software success as evidence of physical readiness [8].

<!-- page: Mechanical design -->

## III. Mechanical design

### A. Frame and packaging basis

The mechanical layout uses a modular PETG frame around an existing 4-inch, 200 mm enclosure. Printed parts are specified as Bambu Lab PETG Basic at 100% infill. Infill is a manufacturing setting, not a proof of isotropic strength or wet-service durability. Structural retention, lifting loads and creep still require physical qualification [4, 7].

The simulator's geometry snapshot records an envelope of 361.920 mm length, 377.472 mm width and 260.000 mm height. Those bounds came from visible CAD bodies and exclude unmodeled wiring, connector movement and tether allowances. The visual assembly is an earlier reference; current mount, stack and endcap exports are separate. The CAD reference point is geometric, not a measured center of mass [9].

### B. Thruster supports and serviceability

Four horizontal U01 thrusters are vectored at 45 degrees to the longitudinal axis; two vertical thrusters provide heave. The current mounts combine each support and rail receiver, removing the obsolete separate clamp jaw from the installed arrangement. Horizontal supports use a smooth solid span; the vertical mounts use rounded receivers. One M4 x 50 mm retainer, one M4 nut and one washer are selected per mount, subject to physical grip-length confirmation [7].

The nominal rail clearance is 0.3 mm over a 60 mm receiver. A coupon is provided because printer variation, surface finish and the actual rail determine fit. CAD checks established solid integrity and local clearance, not retention under reversing thrust. The first physical checks should therefore include insertion force, play, bolt fit and repeatable removal before complete mounts are printed.

<!-- figure: mounts -->

### C. Controller/ESC stack

The adapter places the ESC above the ARK. It bridges the measured 20 x 20 mm ESC pattern and the ARK's 30.5 x 30.5 mm pattern using M3 hardware and four 7 mm spacers. It preserves the ARK isolation arrangement. The exported adapter is 40 x 40 x 6 mm, with a 3 mm web and raised ESC seating bosses [7].

Adapter fit was reported, while complete cable, solder-joint, nut and battery clearance remain unresolved. The wire-management study found an existing FC mounting screw protruding into the Zeee battery envelope. Replacing that screw with a measured, suitable length is a release gate; the battery must not be compressed around an interference.

<!-- figure: stack -->

### D. Endcap is a fit study

The selected layout uses 6.35 mm acrylic, three LP12 motor-connector openings, one LP20 Ethernet opening and two M10 clearance openings for depth sensing and PRV/vacuum access. Six fixing holes and the perimeter produce 13 closed cutting contours. Fit coupons precede the full cut [7].

This is geometry for integration, not a pressure-qualified plate. Connector panel-thickness limits, seal seating, nut engagement, edge spacing and structural response must be checked on the actual hardware. Replacing a thicker blank with 6.35 mm stock and adding openings prevents any inference of unchanged depth capacity. No pressure test or structural safety factor is reported for this endcap.

<!-- page: Electrical design -->

## IV. Electrical design and power

### A. Propulsion and electronics supply

The selected propulsion source is a Zeee 4S 3300 mAh pack, nominally 132 x 43 x 29 mm with a stated +/-2 mm dimensional tolerance. It supplies the propulsion branch and ARK VBAT input. PoE supplies the companion electronics. A backup supply and power-path circuit are not implemented, so the current design must not assume communications survive PoE loss [3, 4].

At a conventional 4S nominal voltage of 14.8 V, nameplate energy is 14.8 V x 3.3 Ah = 48.84 Wh. This is an arithmetic nominal value, not measured usable energy. Runtime requires measured energy delivery under a representative load, an agreed discharge limit and allowance for losses. A fully charged 4S pack reaches 16.8 V; component selection cannot be based on a regulated 14 V assumption.

<!-- figure: power -->

### B. Distribution and removable connections

The proposed distribution uses one insulated positive copper bar and one insulated negative bar, with removable branch connections. It replaces the intermediate PDB/splitter alternatives in the current build selection. Bar dimensions, terminals, protection, wire sizes and enclosure remain provisional; the CAD placeholders are not verified electrical ratings [4, 7].

Six MR30 mated pairs provide internal three-phase motor connections. Four XT60 mated pairs are a provisional battery/ESC harness count. Three LP12 six-pin sets would carry two three-phase thrusters each, while one LP20 set provides removable Ethernet. Counts refer to complete mating connections, not additional halves to be counted again.

Motor phase current cannot be established by dividing battery current by three. Contact loading, wire gauge, temperature rise and simultaneous use must be checked against actual thruster duty. Connector fit or previous use on another vehicle does not establish this assembly's current or depth qualification. Fuse selection and the battery disconnect arrangement remain open decisions.

### C. ESC integration and depth sensing

The DAKEFPV four-in-one serves four motors; two Basic ESCs supply the remaining channels. The selected drone ESC has a measured 20 x 20 mm mounting pattern. An initial Betaflight read identified all four channels as B-X-30 / Bluejay 0.19 / 24 kHz; forward/reverse mode was saved. The installed revision must be confirmed in the as-built configuration export [5].

The ARK startup log establishes DShot300 on outputs 1-4 and PWM on the remaining output group. It does not establish neutral, physical channel mapping or correct thrust direction. Those require individual secured, submerged tests after the MR30 harness is complete.

Bar02 is selected for initial shallow-water pressure sensing over I2C. Its installation, calibration and behavior are pending. It does not cover the entire future 20-50 m ambition; a sensor and enclosure/connector system appropriate to the eventual qualified envelope must be selected before deeper operation. Leak detection and battery voltage/current alarm calibration are also unfinished.

### D. Measured power and thermal budget

No assembled-vehicle power or enclosure-temperature dataset is available. The next measurements separate the propulsion and PoE branches, record voltage with current, and capture idle, video/telemetry, transient and representative loaded conditions. Battery input power is not identical to heat deposited inside the enclosure because motor power leaves through the thrusters. Both internal temperatures and delivered energy must be recorded before assigning endurance [10].

<!-- page: Embedded software -->

## V. Embedded software and communications

### A. Bounded forwarding

Companion firmware 1.2.1 is written in C using ESP-IDF 5.5.2. The source includes a dependency lockfile, a local credential template and portable host checks. The P4 bench board uses 32 MB flash and 32 MB PSRAM; replacement-board revision and configuration must be checked rather than forcing a firmware mismatch [6].

The USB/Ethernet bridge uses bounded queues, reconnect handling and source-endpoint filtering. Its framing helper reconstructs MAVLink frame boundaries; endpoint message CRC/signature validation remains an endpoint responsibility. The bridge is a transport, not another autopilot. Its health counters help distinguish tether loss, USB disconnect, video failure and queue drops rather than treating a single visible indicator as complete system health.

A stale or unavailable operator path must be handled by the intended ArduSub failsafes. The companion cannot guarantee the vehicle stops merely because its own network socket reports a disconnect. A complete post-reset parameter export and demonstrated pilot-input/GCS-link loss behavior remain physical integration tasks [5].

### B. Video pipeline

The IMX219 path captures RAW10 through CSI, passes the image through the ISP and encodes H.264 in hardware. The selected mode crops the binned 1640-pixel width to 1632 pixels, preserving a stride divisible by 16. RTP uses payload type 96, a 90 kHz clock and single-NAL/FU-A packetization with packets below the Ethernet MTU. The encoder is configured for 30 frames/s and 4 Mbit/s; the measured short-run rate was approximately 25 frames/s [6].

Video runs alongside telemetry and maintenance. Camera errors stop the video task rather than being treated as proof of an intact control path. The recorded camera bench run had increasing frame and telemetry counters with zero video errors/send drops in its observation interval. It was not a latency or endurance experiment.

### C. Guarded application updates

The dashboard accepts the companion application image only, using an inactive OTA slot. A recent, CRC-checked autopilot heartbeat from USB must report disarmed; armed or unknown state blocks uploads. Controller state is monitored through the transfer. During upload, command forwarding is inhibited, and the updater does not itself arm or disarm the controller [6].

<!-- figure: ota -->

The layout retains a factory recovery application and two 4 MiB OTA application slots. Startup confirmation requires one minute of operation, an active maintenance HTTP service and at least 128 KiB free heap. Failure to meet checks within two minutes requests rollback. These checks do not establish complete camera, flight-controller or vehicle health.

Uploads use a maintenance password over trusted local HTTP; image integrity is checked, but signed firmware/Secure Boot are not enforced. Successful bench tests include authorization rejection, incompatible/corrupt-image rejection and an uploaded image booting and confirming. Exhaustive interrupted-power and endurance cases are still pending.

### D. Maintainability and ownership

Module boundaries separate Ethernet, USB forwarding, camera/video, heartbeat inspection and OTA. Credentials remain outside source control and are embedded in deployed binaries, which are not published. Initial USB flashing establishes the partition layout; later dashboard updates do not replace it. These choices reduce accidental configuration drift while leaving a documented recovery path for a failed application update.

<!-- page: Simulation -->

## VI. Simulation and pilot training

### A. Geometry-based model

Unity 6000.6.7f1 hosts a six-thruster rigid-body model. The extracted thrust axes and lever arms determine actuator force locations and horizontal allocation. Each thrust line contributes F_i = T_i u_i d_i and moment r_i x F_i about the reference point. A cached minimum-norm allocation maps surge, sway and yaw commands to the horizontal actuators; the vertical pair supplies heave [8, 9].

The body can move in six degrees of freedom, but only four motion axes are directly commanded. Roll and pitch stability arise from the modeled buoyancy offset and damping; the vehicle is not a fully actuated six-axis platform. This distinction matters when comparing it with competition AUV examples.

The present scene uses 8 kg mass, 0.008 m3 displacement, 1000 kg/m3 water density, 15 N full forward thrust per motor, a 0.8 reverse ratio, 0.15 s motor response and 35 mm buoyancy offset. These are explicitly provisional. The nominal mass/displacement pair is neutral when fully submerged in the model; it is not a measured physical trim result.

### B. Two mutually exclusive control modes

Local piloting uses keyboard or gamepad input. SITL transfers actuator ownership to ArduSub 4.7.2, with QGC supplying pilot commands. Unity processes actuator packets and returns IMU specific force, angular rate, NED position/velocity and attitude. A one-second link timeout clears thrust and holds physics until an explicit reconnect. Local and autopilot paths cannot concurrently drive the vehicle [8].

Recorded live exchange settled at 400 Hz after startup. Rendering is separate, and simulation time can lag wall time under load. Simulator depth/heading hold was confirmed through QGC; it does not qualify the physical sensor or real-vehicle tuning. Start/map changes require a coordinated SITL restart.

<!-- figure: fpv -->

### C. Training environments and display

Six layouts provide the original gate/hover/dock course, gate-and-pillar practice, empty pool, slalom, and RoboSub 2026 practice/finals arrangements. The first four use a 20 x 24 x 3.5 m pool; the handbook maps use a 50 x 22.86 x 2.1 m arena. Diagram-derived station spacing is approximate. The handbook stations are static unscored training objects, not implemented competition interactions.

Current, buoyancy bias, single-thruster derating, visibility and camera conditions can be stored as scenarios. Scored runs track time, collisions and depth/heading RMS error; changes to conditions or control mode invalidate competitive score eligibility. This makes exercise conditions and run comparisons repeatable.

Chase, FPV and top-down views support practice. FPV samples a 640 x 480 image at up to 30 frames/s, with optional 0-500 ms image delay. Compact attitude/compass dials, a flight overlay and a Betaflight-style OSD use live simulation truth and are independent of video delay. No noisy AHRS estimator, magnetic model or calibrated underwater image-formation model is implemented.

<!-- page: Verification -->

## VII. Verification and qualification strategy

### A. Recorded results

Evidence is separated into source/host checks, hardware bench observations, geometry checks and simulation checks. None can be substituted for a different category. The companion's recorded host checks delivered 110,979 exact framing cases and 168 heartbeat cases; the CI workflow runs the portable framing/heartbeat tests [11].

Hardware records demonstrate Ethernet/USB telemetry, H.264 reception, simultaneous maintenance service and a successful guarded OTA update. The ESC configuration record demonstrates successful reads through the matching F722, not successful ARK passthrough. Earlier ARK programming attempts returned zero bytes, and the exact cause remains unresolved. A corrected harness resolved an F722 power/USB symptom, illustrating why a matching plug is not evidence of correct pin assignment [5, 6].

Simulator records include six mixer, eleven motion/lifecycle, twelve synthetic input/camera, nineteen course/visibility, ten FPV and twelve each protocol/link checks. Later checks address scenario persistence, six maps, artwork, panel behavior and instrument styles. These are bounded tests, not statistical mission-success rates. Windows builds and selected standalone startup/actions were also checked [8].

<!-- table: evidence -->

### B. Qualification gates

Gate 1 is an unpowered inspection: actual pin traces, cable continuity, battery clearance, complete fastener selection and print fit. Gate 2 establishes stable power, known neutral outputs, calibration and operator-input recognition with propulsion initially disabled. Individual low-power motor tests then identify position and both thrust directions with the vehicle secured and submerged.

Gate 3 verifies failure behavior: operator-input loss, GCS/tether loss, USB reconnect, PoE interruption and battery power loss must produce the intended controller behavior and a documented recovery procedure. A companion counter or a simulation timeout is insufficient evidence of physical failsafe action.

Gate 4 qualifies the enclosure and penetrators independently of valuable electronics through a reviewed procedure with stated pressure/leak criteria, instrumentation and abort conditions. Gate 5 performs shallow-water trim, controlled motion and recovery tests. Expanded depth/endurance requires successful preceding gates and compatible component limits [10].

### C. Principal unresolved risks

The immediate mechanical risk is the screw/battery interference; the primary containment risk is the unqualified 6.35 mm endcap and connector interfaces. Electrical risks include unselected branch protection, phase-contact loading and harness mistakes. Control risks include unresolved physical motor mapping, neutral and input/link-loss responses. Integration scheduling should close these items before adding autonomy features.

The report therefore treats current completion as an integration draft. The strongest demonstrated capability is the communications/video and software-training stack; physical vehicle qualification is the next critical path. No numerical reliability, pressure safety factor, mission success percentage or field endurance is assigned without a supporting experiment.

<!-- page: Release plan and references -->

## VIII. Release measurements and conclusions

### A. Measurements required for revision 1.0

The next report revision needs a complete as-built configuration: final combined CAD export, actual motor numbering/directions, credential-free controller parameter export and installed harness drawing. A current BOM identifies selected components but does not yet resolve all fastener, wire, protection and sealing details [4].

Measure dry mass and center of mass with the final battery, wiring and accessories fitted; measure displacement, buoyancy and trim in water. Identify thrust-versus-command in both directions and use it to replace the simulator's assumed 15 N/0.8 profile. Record speed/current and transient response under controlled conditions before claiming drag or inertia calibration.

Record propulsion and PoE power separately, useful delivered energy, enclosure/component temperature, video latency and test duration. Establish accepted depth and leak-test conditions through the qualification gates. Cost totals should be based on verified purchases and reused-equipment accounting, not fabricated estimates. These measurements are prerequisites for release specifications.

### B. Design conclusions

Club ROV combines a modular mechanical arrangement with a deliberate separation between flight control and communications. A low-level companion supports useful telemetry/video and maintenance without taking over the vehicle control loop. The Unity/SITL platform provides repeatable pilot exercises and integration checks while the physical harness and enclosure are completed.

Printed packaging and removable connections support iteration and servicing; they still require electrical and pressure qualification. Report maturity depends on closing those physical gates and replacing simulator assumptions with measurements. The next milestone is a documented shallow-water operating envelope, followed by deliberate sensing and autonomy extensions.

## References and evidence

[1] Ohio State Underwater Robotics Team, RoboSub 2026 Technical Design Report. Structural example: mission objectives, subsystem choices and testing strategy. [Official report](https://robonation.org/app/uploads/sites/4/2026/07/RS26_TDR_TheOhioState_UWRT.pdf)

[2] NUS Bumblebee Autonomous Systems, RoboSub 2026 Technical Design Report. Structural example: capability/design alignment, risk discussion and staged testing. [Official report](https://bumblebee.sg/pdf/Bumblebee_Robosub_Paper_2026.pdf)

[3] Club ROV, System architecture. [Project source](../ARCHITECTURE.md).

[4] Club ROV, Current bill of materials and hardware status. [BOM](../BOM.md); [status](../STATUS.md).

[5] Club ROV, Electronics setup and ESC recovery. [Project source](../ELECTRONICS.md).

[6] Club ROV, Companion firmware 1.2.1: camera, Wi-Fi and OTA implementation/bench records. [Firmware guide](../../Code/ROV%20ESP32-P4%20code/README.md); [video](../../Code/ROV%20ESP32-P4%20code/VIDEO.md); [OTA](../../Code/ROV%20ESP32-P4%20code/OTA.md).

[7] Club ROV, Mechanical exports and fit-study records. [Hardware index](../../Hardware/README.md).

[8] Club ROV, Unity training and SITL validation. [Simulator](../../Simulation/ClubROVSim/README.md); [validation](../../Simulation/ClubROVSim/Docs/VALIDATION.md); [SITL](../../Simulation/SITL/README.md).

[9] Club ROV, Fusion geometry snapshot, 23 October 2026. [Measurements](../../Simulation/ClubROVSim/Docs/FUSION_GEOMETRY.md); [machine-readable snapshot](../../Simulation/ClubROVSim/Docs/fusion-geometry.json).

[10] Club ROV, Power measurement procedure and physical qualification plan. [Power](../POWER_MEASUREMENT.md); [testing](../TESTING.md).

[11] Club ROV, Repository validation and CI checks. [Validation](../REPOSITORY_VALIDATION.md); [workflow](../../.github/workflows/host-checks.yml).

Reference papers [1, 2] were accessed on 26 October 2026. Their role is organizational guidance; the technical narrative, diagrams and evidence mapping in this report are original to Club ROV. Existing CAD reference geometry retains the attribution in the project publishing notes. No institutional affiliation, unrecorded test or competition qualification is implied.


<!-- appendix: measurements -->

The closeout record distinguishes geometry, nominal calculations and physical measurements. Only the evidence state shown is supported. Proposed measurements should identify instrument, configuration, uncertainty and acceptance criteria before a result is approved.

Finished mass, center of mass, displacement, trim, thrust curves, useful energy, thermal behavior, end-to-end latency, qualified depth and actual cost remain pending. A simulated 8 kg body or a nominal 48.84 Wh pack calculation must not be entered as a measured result.
