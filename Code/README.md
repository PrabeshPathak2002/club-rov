# Companion firmware workspace

Firmware is not implemented yet. This folder contains local ESP-IDF setup configuration, not a runnable ROV application.

The planned target is the Waveshare ESP32-P4-WIFI6-POE-ETH. It will handle Ethernet, the camera, and USB-host MAVLink communication with the ARK FPV running ArduSub. The ARK owns thruster control.

Implementation awaits remaining software requirements and hardware decisions. See the [architecture](../Documentation/ARCHITECTURE.md) and [roadmap](../Documentation/ROADMAP.md).

When firmware is added, record the board revision, ESP-IDF version, dependencies, build/flash/monitor commands, settings, expected startup output, and bench-test procedure. Do not publish credentials or local machine paths. Initial ignore rules admit only this README from this folder; extend them deliberately when code is ready.
