# System architecture

Status: planned electrical architecture, recorded 29 September 2026. This is an explanation of responsibilities, not a validated wiring guide.

## Control and data

The intended command path is topside operator → Ethernet tether → ESP32-P4 → USB MAVLink → ARK FPV running ArduSub → six ESC channels → thrusters. Telemetry returns in the opposite direction. Camera data is intended to reach the surface through the ESP32 and Ethernet.

The ARK owns motor control and vehicle failsafes. The ESP32 is a companion responsible for communications and camera handling. Its USB host driver and MAVLink bridge are not implemented. Camera interface, driver, stream format, and achievable latency remain to be established.

The project owner has used the ARK with ArduSub in previous submarines; this vehicle still requires integration testing. Depth, sonar, and other sensors are deliberately undecided. For each sensor, record its power, interface, calibration, timestamps, logging format, and whether the ARK or companion will own it.

## Mechanical platform

Six APISQUEEN U01 thrusters use four horizontal and two vertical mounts. The PETG frame retains the existing 4-inch, 200 mm enclosure. Printing is planned on an H2D with 100% infill as the requested starting point. Print orientation, joints, sealing, and loads still need physical validation. The 20–50 m operating depth remains a target.

## Power

- A 4S 4000 mAh battery is planned for propulsion and the ARK battery input through the ESC harness.
- DakeFPV lists the GT 6S 55A with 3–6S input and VBAT output. ARK lists a 5.5–54 V VBAT input. These ranges support the proposed feed; cable pin order is unverified.
- Two additional ESCs supply channels five and six; exact models are TBD.
- PoE powers the companion and accessories. Backup battery chemistry, capacity, charging, power-path circuitry, and switchover behavior remain TBD.
- Simultaneous ARK VBAT and USB power behavior needs verification. Backup power cannot restore a severed Ethernet link.

## Open decisions and checks

1. ESC connector wiring and reversible thrust operation; bidirectional DShot wording alone does not confirm reverse thrust.
2. USB enumeration, power interaction, MAVLink routing, and reconnect behavior.
3. Camera compatibility and video/control latency under load.
4. Power consumption, PoE budget, battery endurance, and thermal behavior.
5. Tether, penetrators, sealing, buoyancy, ballast, and depth qualification.
6. Topside software, operator controls, and explicit ArduSub link-loss behavior.

## Primary references

- [DakeFPV GT 6S 55A](https://www.dakefpv.com/pd.jsp?id=97)
- [ARK FPV pinout](https://docs.arkelectron.com/products/flight-controller/ark-fpv/pinout)
- [Waveshare ESP32-P4-WIFI6-POE-ETH](https://docs.waveshare.com/ESP32-P4-WIFI6-POE-ETH)
- [Espressif USB CDC host](https://components.espressif.com/components/espressif/usb_host_cdc_acm)

Match documentation to the actual hardware revision before preparing wiring instructions.
