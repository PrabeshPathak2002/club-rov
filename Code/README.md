# Software

The [ESP32-P4 companion firmware](ROV%20ESP32-P4%20code/README.md) handles Ethernet/USB MAVLink, IMX219 H.264 video, Wi-Fi maintenance dashboard and OTA. Use ESP-IDF 5.5.2 and the dependency lockfile.

The ARK runs upstream ArduSub 4.7.2. [Electronics setup](../Documentation/ELECTRONICS.md) records the current partial configuration. A complete post-reset parameter export is still required.

ROS 2 Humble on Ubuntu 22.04 is planned for autonomy and sensor experiments. Autonomy nodes and the complete surface setup are not implemented.
