# Build and test notes — 23 October 2026

This page records physical-hardware bench results and outstanding work as of 23 October 2026. The vehicle has not completed water or depth qualification. Software-only [SITL results](../Simulation/SITL/README.md#validation), including joystick and depth/heading-hold checks, are tracked separately and do not complete the physical-vehicle tests listed here.

## Bench results

- **Telemetry:** The ESP32-P4 connects to the ARK over USB and forwards MAVLink over Ethernet. QGroundControl has maintained a stable connection during bench use with companion firmware 1.2.1.
- **Camera:** The Raspberry Pi Camera V2.1 is streaming H.264 video to QGroundControl. A short bench run produced approximately 25 fps at 1632 × 1232 with a configured bitrate of 4 Mbit/s. Latency and endurance measurements remain pending.
- **Maintenance Wi-Fi:** The dashboard was checked while Ethernet video and telemetry continued running.
- **Firmware updates:** A real OTA upload booted from an OTA slot and passed its startup confirmation. Bench checks also rejected invalid authorization and invalid images. The [OTA notes](../Code/ROV%20ESP32-P4%20code/OTA.md) explain the checks and their limits.
- **Four-in-one ESC:** The matching F722 running Betaflight 4.5.2 read all four ESCs as `B-X-30 - Bluejay, 0.19, 24kHz`. Forward/Reverse (3D mode) was then saved on all four.
- **ARK configuration:** After resetting the parameters and rebuilding the setup, the startup log shows ArduSub 4.7.2, `Frame: VECTORED` and `RCOut: DS300:1-4 PWM:5-9`.

## ESC troubleshooting

During F722 setup, its USB port disappeared and its blue LED went out when the propulsion battery was connected. The fault was traced to the harness wiring. After correcting it, ESC Configurator could read all four channels through Betaflight.

Earlier ARK passthrough attempts returned zero bytes from every ESC. The exact cause of that failure remains unresolved. Successful configuration through Betaflight establishes that the ESC programming interfaces work, but normal motor control through the ARK remains untested.

## Next steps

- Install the six MR30 connector pairs once they arrive, then verify polarity, insulation and channel mapping.
- Check that all thrusters stay stopped at neutral, then test each channel and both thrust directions with the vehicle secured and submerged.
- Finish joystick setup and verify manual-control input. The last startup screenshot included `Lost manual control`, which remains unresolved.
- Test pilot-input loss, GCS-link loss and power-loss behavior.
- Install Bar02 and leak detection. A Bar02 is available for shallow-water work, but its roughly 10 m measuring range will not cover the full 20–50 m target.
- Check connector coupons, seal seating, panel thickness and nut engagement before cutting and qualifying the endcap.
- Replace the long FC mounting screw that protrudes into the Zeee battery envelope.
- Measure power draw, internal temperatures, video latency, endurance, buoyancy and trim.
- Add ROS 2 and define how manual and autonomous control will share command authority.

Add dated [test reports](templates/TEST_REPORT.md) as these checks are completed. MR30 installation, propulsion tests and water trials are still pending.
