# Software

My [ESP32-P4 companion firmware](ROV%20ESP32-P4%20code/README.md) handles Ethernet/USB MAVLink, IMX219 H.264 video, Wi-Fi maintenance dashboard and OTA. Use ESP-IDF 5.5.2 and the dependency lockfile.

I use upstream ArduSub 4.7.2 on the ARK. [Electronics setup](../Documentation/ELECTRONICS.md) records the current partial configuration. I still need to capture a complete post-reset parameter export.

I plan to use ROS 2 Humble on Ubuntu 22.04 for future autonomy and sensor experiments. I have not added the autonomy nodes or a complete ROS surface setup yet.
