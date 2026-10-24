# Fusion geometry — 23 October 2026

These dimensions were extracted directly from the open **Future ROV** assembly, including its unsaved state, without changing or saving the Fusion document. The active document was not named Club_Demo_ROV. The exact source name is retained for traceability.

The visible modeled envelope is **361.920 mm long × 377.472 mm wide × 260.000 mm high**. The measurement uses the union of precise bounding boxes of 1,914 visible BRep bodies, excluding hidden alternatives. Unmodeled cables, connectors and tether do not contribute to these dimensions.

All four horizontal U01 axes lie in the horizontal plane at **45° to the forward axis**. Both vertical U01 axes are perpendicular to that plane. The analytic shroud-cylinder radius is 31 mm.

## Reference and measurements

The geometric reference is the center of the visible assembly bounding box: CAD **(0.857, 0, 126.000) mm**. CAD axes are X forward, Y port, Z up. This point is not a measured center of mass.

The following axis points are relative to that reference, expressed as **forward, port, up** in millimeters:

- Rear starboard: **(-133.937, -128.920, -16.000)**.
- Rear port: **(-133.937, +128.920, -16.000)**.
- Front starboard: **(+130.509, -127.206, -16.000)**.
- Front port: **(+133.937, +130.634, -16.000)**.
- Vertical starboard: **(-3.281, -152.000, -14.472)**.
- Vertical port: **(+1.567, +152.000, -14.472)**.

Each force location uses a point on the analytic 31 mm-radius shroud-cylinder axis, rather than an inferred propeller center. For a force along that axis, moving the application point along the same line does not change its moment. The cylinder axis sign is a CAD convention; positive motor thrust and ArduSub channel mapping still require physical verification.

The [machine-readable snapshot](fusion-geometry.json) preserves the source occurrence paths, raw CAD axis points/directions, coordinate conversion and full precision. Unity coordinates are X starboard, Y up, Z forward, in meters.

## Printed parts and remaining calibration

All project 3D printed parts use **Bambu Lab PETG Basic at 100% infill**. The earlier 10% statement was corrected before recording this snapshot. This agrees with the existing 100% baseline print guide.

A CAD solid-mass estimate has not been substituted for the finished vehicle weight. Battery, wiring, hardware, printed-part slicing and actual water displacement still need to be accounted for. The simulator's 8 kg mass, buoyancy, drag and thrust values remain provisional.

The six extracted axis lines now drive `RovThrusterLayout`, and the measured envelope drives the collider and partial-submersion height. The horizontal allocation uses the measured yaw lever arms. Scene force markers are updated through Unity's Editor API. The visual mesh remains the existing September reference. Six mixer checks and eleven Play Mode motion/lifecycle checks passed after integration; measured mass/balance and physical motor-direction verification remain outstanding.
