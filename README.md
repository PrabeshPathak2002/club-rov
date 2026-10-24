# Club ROV

I'm building this six-thruster ROV for my robotics club as a platform for training new members, testing sensors for our RoboSub AUV, and running outreach demonstrations. It combines a modular printed frame, an ARK flight controller running ArduSub, and an ESP32-P4 for camera streaming and Ethernet communication.

[View the detailed assembly in 3D](https://github.com/PrabeshPathak2002/club-rov/blob/main/Models/club-rov-preview.stl) · [New member guide](Documentation/NEW_MEMBERS.md) · [Current status](Documentation/STATUS.md)

[![Club ROV assembly](Models/preview.png)](https://github.com/PrabeshPathak2002/club-rov/blob/main/Models/club-rov-preview.stl)

[![View in 3D](https://img.shields.io/badge/View_in_3D-0969DA?style=for-the-badge)](https://github.com/PrabeshPathak2002/club-rov/blob/main/Models/club-rov-preview.stl)

The 3D preview shows my September frame revision. I have since revised the mounts, battery layout, electronics stack and endcap; those changes are documented under [Hardware](Hardware/README.md) and are not all included in the preview yet.

## System

I split vehicle control and communications between two boards: the ARK handles stabilization and thruster mixing, while the ESP32 handles video, telemetry transport and maintenance.

- Six APISQUEEN U01 thrusters: four vectored horizontal and two vertical.
- ARK FPV with ArduSub: stabilization, motor mixing and vehicle failsafes.
- Waveshare ESP32-P4-WIFI6-POE-ETH: USB MAVLink bridge, camera video, maintenance dashboard and OTA updates.
- Raspberry Pi Camera V2.1 / IMX219: hardware H.264 video over Ethernet.
- DAKEFPV four-in-one ESC plus two Blue Robotics Basic ESCs.
- Propulsion battery: Zeee 4S 3300 mAh, 132 × 43 × 29 mm nominal. PoE supplies companion electronics; backup power remains to be designed.
- QGroundControl at the surface. I plan to add ROS 2 Humble on Ubuntu 22.04 for sensor experiments and tethered autonomy.

## Build, learn and contribute

I am documenting the build so new club members can understand the design, reproduce the software setup and contribute to the project.

- [Firmware build and source tour](Code/ROV%20ESP32-P4%20code/README.md)
- [Architecture and network setup](Documentation/ARCHITECTURE.md)
- [Electronics and ESC setup](Documentation/ELECTRONICS.md)
- [Current component list](Documentation/BOM.md)
- [Mechanical revisions and fit files](Hardware/README.md)
- [Unity pool simulator](Simulation/ClubROVSim/README.md)
- [ArduSub SITL and Unity setup](Simulation/SITL/README.md)
- [Testing plan](Documentation/TESTING.md) and [bench results](Documentation/STATUS.md)
- [New member learning path](Documentation/NEW_MEMBERS.md)
- [Roadmap](Documentation/ROADMAP.md) and [contribution guide](CONTRIBUTING.md)
