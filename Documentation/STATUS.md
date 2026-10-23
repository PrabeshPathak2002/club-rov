# Build and test notes — 23 October 2026

These are my current bench results and the work I still need to finish. The vehicle has not completed water or depth qualification.

## Bench results

- **Telemetry:** I have the ESP32-P4 connected to the ARK over USB and sending MAVLink over Ethernet. QGroundControl has maintained a stable connection during bench use with companion firmware 1.2.1.
- **Camera:** The Raspberry Pi Camera V2.1 is streaming H.264 video to QGroundControl. A short bench run produced approximately 25 fps at 1632 × 1232 with a configured bitrate of 4 Mbit/s. I still need latency and endurance measurements.
- **Maintenance Wi-Fi:** I checked that the dashboard works while Ethernet video and telemetry continue running.
- **Firmware updates:** A real OTA upload booted from an OTA slot and passed its startup confirmation. Bench checks also rejected invalid authorization and invalid images. The [OTA notes](../Code/ROV%20ESP32-P4%20code/OTA.md) explain the checks and their limits.
- **Four-in-one ESC:** I used my F722 running Betaflight 4.5.2 to read all four ESCs as `B-X-30 - Bluejay, 0.19, 24kHz`, then saved Forward/Reverse (3D mode) on all four.
- **ARK configuration:** After resetting the parameters and rebuilding the setup, the startup log shows ArduSub 4.7.2, `Frame: VECTORED` and `RCOut: DS300:1-4 PWM:5-9`.

## ESC troubleshooting

When I connected the F722, its USB port disappeared and its blue LED went out as soon as I plugged in the propulsion battery. I traced this to the harness wiring. After correcting it, ESC Configurator could read all four channels through Betaflight.

My earlier ARK passthrough attempts returned zero bytes from every ESC. I have not isolated the exact cause of that failure. Successful configuration through Betaflight establishes that the ESC programming interfaces work, but I still need to test normal motor control through the ARK.

## Next steps

- Install the six MR30 connector pairs once they arrive, then verify polarity, insulation and channel mapping.
- Check that all thrusters stay stopped at neutral, then test each channel and both thrust directions with the vehicle secured and submerged.
- Finish joystick setup and verify manual-control input. The last startup screenshot included `Lost manual control`, which I still need to resolve.
- Test pilot-input loss, GCS-link loss and power-loss behavior.
- Install Bar02 and leak detection. I have a Bar02 for shallow-water work, but its roughly 10 m measuring range will not cover the full 20–50 m target.
- Check connector coupons, seal seating, panel thickness and nut engagement before cutting and qualifying the endcap.
- Replace the long FC mounting screw that protrudes into the Zeee battery envelope.
- Measure power draw, internal temperatures, video latency, endurance, buoyancy and trim.
- Add ROS 2 and define how manual and autonomous control will share command authority.

I will add dated [test reports](templates/TEST_REPORT.md) as these checks are completed. MR30 installation, propulsion tests and water trials are still pending.
