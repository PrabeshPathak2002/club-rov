# Thruster corner display

The code-rendered schematic uses the six-motor BlueROV2 **Vectored** frame diagram as its visual reference: [ArduSub frame configurations](https://ardupilot.org/sub/docs/sub-frames.html), [reference diagram](https://ardupilot.org/sub/_images/vectored-frame.png). It uses a rounded deck, bow triangle, four diagonal horizontal pods and two vertical propellers, with the reference's green/blue propeller groups. It is a schematic, not a dimensioned model of Club ROV or a copy of the eight-thruster Heavy configuration.

Labels retain Club ROV names alongside ArduSub motor numbers: front port M2/H1, front starboard M1/H2, rear port M4/H3, rear starboard M3/H4, port vertical M6/V1 and starboard vertical M5/V2. These associations match the existing `RovSitlProtocol.Apply` mapping. No motor channel, force direction or firmware parameter is changed.

Percentages are the existing `RovDynamics.Output` simulated force fractions, including derating; they are not raw MAVLink PWM signs. Cyan/amber activity rings and horizontal arrows show positive/negative force using the actual simulator thrust axes. A dot/cross on vertical thrusters shows upward/downward force. Green/blue housing colors remain static reference propeller groups and do not indicate live thrust sign.

The diagram is drawn with UI Toolkit Painter2D and needs no downloaded image at runtime. It follows the existing H/MENU visibility and HUD lifecycle. Validation: clean Unity compilation and live Game-view inspection of all six motor labels and pod positions.
