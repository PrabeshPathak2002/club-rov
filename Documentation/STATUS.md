# Integration status — 23 October 2026

This record distinguishes user-reported bench results, captured software output and outstanding physical tests. It is not a complete vehicle qualification.

## Recorded results

- ESP32-P4 firmware 1.2.1: Ethernet telemetry and USB connection to ARK worked on the bench; the user confirmed stable QGroundControl operation.
- IMX219: chip identity detected; camera capture, hardware H.264 and RTP/UDP video operated at approximately 25 fps, 1632 × 1232, configured 4 Mbit/s. Latency and endurance are unmeasured.
- Wi-Fi: the user confirmed dashboard, Ethernet video and telemetry working together.
- OTA: a real uploaded application booted from an OTA slot and passed its startup confirmation. Invalid authorization/images were rejected in bench checks. See the firmware OTA notes for the limits of these checks.
- DAKEFPV four-in-one: ESC Configurator through a Betaflight 4.5.2 F722 read all four as `B-X-30 - Bluejay, 0.19, 24kHz`. The user reported saving Forward/Reverse (3D mode) on all four.
- ARK after parameter reset/setup: screenshot reports ArduSub 4.7.2, `Frame: VECTORED`, `RCOut: DS300:1-4 PWM:5-9`.

## Important troubleshooting result

An F722 harness mismatch caused its USB port to disappear and blue LED to go out when battery power was applied. The user corrected the wiring, after which all four ESCs could be read through Betaflight. The earlier ARK passthrough attempts returned zero bytes from every channel. Their exact cause has not been isolated. Do not describe the earlier programming failure as proof of an unsupported ESC or failed motor control.

## Pending

- Install the six MR30 connector pairs; independently verify every powered connector's polarity and mapping.
- Confirm all ESCs stay stopped at neutral; then verify each motor channel, forward/reverse response and physical thrust direction with a secured, submerged vehicle.
- Configure/calibrate the surface joystick. A startup screenshot showed `Lost manual control`; resolution has not been demonstrated.
- Validate link-loss, pilot-input loss and power-loss behavior; do not infer failsafe performance from normal telemetry.
- Install and configure Bar02 and leak detection. Bar02 is a roughly 10 m measuring-range device and is not suitable for measuring the full 20–50 m target.
- Fit-check connector coupons, actual seals, panel thickness and nut engagement. Qualify the endcap/enclosure separately.
- Verify shorter FC mounting hardware clears the Zeee battery; the layout study found a protruding screw.
- Measure power, internal temperature, video latency, endurance, buoyancy and trim.
- Integrate ROS 2 and define command ownership before autonomous operation.

MR30 installation, propulsion tests and water/depth trials are not marked complete. Future results belong in dated [test reports](templates/TEST_REPORT.md).
