# System architecture

Updated 23 October 2026. For demonstrated capabilities and pending tests, see [status](STATUS.md).

## Responsibilities and data flow

Surface QGroundControl → Ethernet tether → ESP32-P4 UDP/USB bridge → ARK FPV running ArduSub → ESCs → six thrusters. Vehicle telemetry returns over the same path. The IMX219 camera connects to the P4's CSI interface; its hardware H.264 stream goes to QGroundControl over Ethernet.

ARK owns stabilization, mixing and vehicle failsafes. The companion transports messages and video and serves local diagnostics/OTA. It does not generate vehicle heartbeats, arm the ROV or implement an emergency stop.

## Network

- ROV Ethernet: `192.168.137.2/24`.
- Surface adapter: `192.168.137.1/24`.
- MAVLink: P4 UDP `14555` ↔ surface UDP `14550`; only the configured surface IP and source port are accepted.
- Video: H.264 RTP to surface UDP `5600`.
- Maintenance Wi-Fi: `ROV-Maintenance`, dashboard `http://192.168.4.1`; also available at `http://192.168.137.2` over the tether.
- The hotspot is for above-water maintenance. It does not route vehicle commands, stream video or provide Internet access.

Use the P4 Type-A host port for the ARK USB device connection. The observed ARK VID/PID is `1209:5740`; interface 0 carries MAVLink and the separate SLCAN interface must not be selected. The source defaults allow any single CDC device; set the VID/PID in menuconfig if you need to restrict enumeration.

QGroundControl receives the UDP H.264 stream on port 5600. For future ROS 2 Humble use, MAVLink Router can fan out the one surface endpoint to QGC and MAVROS. Routing alone does not arbitrate manual/autonomous command ownership. ROS integration is not implemented.

## Power and propulsion

The selected 4S Zeee 3300 mAh pack supplies propulsion and the ARK through the verified VBAT/GND harness. Four-in-one ESC signals use ARK outputs 1–4; two Basic ESCs use 5–6. A custom removable copper-busbar arrangement is planned. Connector and wire ratings must cover actual circuit current; motor phase current is not battery current divided by three.

PoE powers the ESP32 and its accessories. A backup battery and power-path circuit are proposed, not implemented. Backup power cannot restore a broken Ethernet tether. Battery voltage/current sensing and alarm calibration remain to be completed.

## Mechanics and sensors

The existing 4-inch, 200 mm enclosure sits in a modular PETG frame. [Hardware](../Hardware/README.md) identifies current custom-part exports, an ESC-above-FC stack, the Zeee packaging study and quarter-inch endcap fit geometry.

Bar02 is owned and selected for initial shallow-water depth sensing over I2C via the GPS port. Other sensors and leak detection are not installed. The 20–50 m target needs a depth sensor and enclosure/connectors qualified for that range.

## References

- [ARK FPV hardware and pinout](https://docs.arkelectron.com/products/flight-controller/ark-fpv/hardware)
- [Waveshare ESP32-P4 board](https://docs.waveshare.com/ESP32-P4-WIFI6-POE-ETH)
- [ArduSub ESC support](https://ardupilot.org/sub/docs/common-blheli32-passthru.html)
- [Bluejay settings](https://github.com/bird-sanctuary/bluejay/wiki/Settings)
- [Bar sensor guide](https://bluerobotics.com/learn/bar-sensors-guide/)

Match the physical board revision and printed pin labels before wiring. Matching plugs and wire colours do not establish a compatible pinout.
