# Roadmap

This roadmap separates recorded milestones from outstanding physical integration, simulator calibration and autonomy work. Completed software checks do not establish physical vehicle qualification.

## Recorded milestones

- [x] Mechanical frame baseline and later custom-part revisions documented.
- [x] ESP-IDF companion source and dependency lockfile prepared.
- [x] Ethernet and USB MAVLink telemetry.
- [x] IMX219 capture and H.264 video received by QGroundControl.
- [x] Wi-Fi maintenance dashboard and real OTA update with startup rollback.
- [x] Read all four DAKEFPV ESCs through Betaflight and save forward/reverse mode.
- [x] ARK Vectored frame and mixed DShot/PWM startup confirmed.
- [x] Implement the Unity six-thruster training simulator, six maps, adjustable water conditions, selectable FPV instruments and portable Windows packaging. See the [simulator validation history](../Simulation/ClubROVSim/Docs/VALIDATION.md).
- [x] Connect Unity to ArduSub SITL through QGroundControl; confirm manual control, depth hold and heading hold in software-only tests. See the [SITL validation](../Simulation/SITL/README.md#validation).

## Next integration work

- [ ] Install MR30s and inspect the complete harness.
- [ ] Verify neutral, motor mapping and forward/reverse thrust in water.
- [ ] Complete calibration, joystick setup and full parameter export.
- [ ] Validate pilot/GCS link-loss and power-loss behavior.
- [ ] Fit actual battery, shorter FC fasteners, connectors and wiring clearances.
- [ ] Integrate Bar02 and leak detection; select a deeper-range sensor before deeper trials.
- [ ] Validate endcap coupon fits, seals, panel/nut engagement and pressure design.
- [ ] Measure power, thermal behavior, latency and endurance.

## Qualification and learning

- [ ] Shallow-water trim, buoyancy, controllability and recovery tests.
- [ ] Establish demonstrated depth through supervised progressive qualification.
- [ ] Implement ROS 2 Humble routing, logging and explicit manual/autonomy command ownership.
- [ ] Calibrate Unity vehicle dynamics against measurements of the assembled ROV.
- [ ] Integrate ROS 2 with the simulator and define its command-ownership boundary with QGroundControl.
- [ ] Develop supervised autonomy and sensor experiments.
- [ ] Publish measured results and a portfolio case study, crediting contributors.
- [ ] Finish third-party CAD attribution and agree on licenses with the contributors.
