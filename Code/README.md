# Software

[ESP32-P4 companion](ROV%20ESP32-P4%20code/README.md) is the current firmware source: Ethernet/USB MAVLink, IMX219 H.264 video, Wi-Fi maintenance dashboard and OTA. Use ESP-IDF 5.5.2 and the dependency lockfile.

The ARK runs upstream ArduSub 4.7.2; its firmware is not forked here. [Electronics setup](../Documentation/ELECTRONICS.md) records the current partial configuration. A complete post-reset parameter export is still needed.

ROS 2 Humble / Ubuntu 22.04 integration is planned. No autonomy node or complete surface installation is included yet.
