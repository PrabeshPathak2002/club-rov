# Electronics setup and ESC recovery

The control system uses an ARK FPV with ArduSub 4.7.2 and the six-thruster Vectored frame. The DAKEFPV four-in-one connects to outputs 1–4, and the Basic ESCs connect to outputs 5–6. FC parameters were reset on 23 October 2026 before applying the output setup below. A complete parameter export remains pending.

## Wiring first

Disconnect battery and USB before swapping harnesses or checking continuity. Trace VBAT, ground and each signal by the board labels at both ends. The ARK harness and F722 harness must be verified independently. An original FC cable is not necessarily wired for a different ESC.

ARK's eight-pin PWM/UART4 connector: 1 VBAT input, 2 external current sense, 3 UART4 RX, 4 PWM1, 5 PWM2, 6 PWM3, 7 PWM4, 8 GND. Use the manufacturer's numbered-view orientation. On this ESC, C is used as the current-sense label; the exact purpose of N has not been verified and it must not be assumed to be a required telemetry wire. Do not connect it to a supply.

The six-pin GPS connector provides 5 V on pin 1, I2C SCL on 4, SDA on 5, and GND on 6 for Bar02. Pins 2/3 are UART and unused for that sensor. Check the exact Bar02 interface and pin numbering before making a harness.

## Four-in-one ESC configuration

All four channels were read through the matching F722 running Betaflight 4.5.2. They already had B-X-30 / Bluejay 0.19 / 24 kHz installed, allowing forward/reverse configuration without a firmware flash.

With motors disconnected, select **Motor Direction → Forward/Reverse (3D mode)** on all four, write settings and read back. Use Brake on Stop off as the initial setup. Keep timing and startup power unchanged until tested. The plain Reversed choice changes a single rotation direction; it does not enable reversible thrust. Do not click Flash All ESCs merely to save settings.

## ARK output configuration

Select the six-motor Vectored frame and confirm FC orientation. With propulsion power disconnected, set:

```text
SERVO_BLH_MASK     15
SERVO_BLH_OTYPE    5     # DShot300; verify the displayed enum
SERVO_BLH_3DMASK   15
SERVO_BLH_BDMASK   0
SERVO_BLH_RVMASK   0
SERVO_BLH_TEST     0
SERVO_BLH_DEBUG    0
MOT_PWM_TYPE      0     # Normal PWM; verify the displayed enum
```

Numeric mask 15 selects channels 1, 2, 3 and 4; it does not mean channel 15. 3DMASK enables forward/reverse DShot mapping. BDMASK is for RPM feedback and is a different feature. Leave outputs 5–6 as ordinary PWM for the Basic ESCs.

Save, reboot and check for `RCOut: DS300:1-4 PWM:5-9`. This startup line was recorded after resetting and configuring the ARK. It proves output protocol assignment only, not that the motors are connected, stopped at neutral, mapped correctly or producing correct thrust. Read back 3DMASK as well; it is not established by that startup line.

## Test sequence after MR30 installation

1. Inspect solder joints, insulation and strain relief; trace all six motor connections. Remove exposed loose ends.
2. Confirm calibration, FC orientation, selected frame and joystick input with propulsion disconnected. Resolve Lost manual control and configure intended link/pilot-input failsafes.
3. In a supervised setup, power the disarmed vehicle with all motor commands neutral. No thruster should run continuously. Disconnect power if one does.
4. Secure the vehicle with thrusters submerged and hands clear. Use individual low-power QGC motor tests; record which physical thruster responds and both thrust directions. Do not assume channel numbers equal vehicle positions.
5. Record corrections, a full parameter export and test results. Verify failure behavior before free-swimming trials.

Do not bypass arming checks simply to clear an error. Connection and testing of all six thrusters remain pending delivery of the MR30 connectors.

## Troubleshooting

- COM disappears / FC LED goes out on battery connection: disconnect power and inspect the harness and rails. This symptom occurred on the F722 and was resolved by correcting the harness wiring.
- ARDU 3.3.1 in ESC Configurator: an MSP compatibility identifier, not proof the installed ArduSub is 3.3.1.
- ARK passthrough short reads: all four tests returned zero bytes. The cause remains unresolved; successful Betaflight reads do not by themselves prove ARK passthrough compatibility.
- Two ARK serial ports: use the MAVLink interface, not SLCAN; close competing serial applications.

Sources: [ARK hardware](https://docs.arkelectron.com/products/flight-controller/ark-fpv/hardware), [ArduSub ESC setup](https://ardupilot.org/sub/docs/common-blheli32-passthru.html), [Bluejay settings](https://github.com/bird-sanctuary/bluejay/wiki/Settings).
