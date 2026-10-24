# Fusion geometry — 23 October 2026

I extracted these dimensions directly from the open **Future ROV** assembly, including its unsaved state, without changing or saving the Fusion document. The active document was not named Club_Demo_ROV. I retained the exact source name so this snapshot can be traced.

The visible modeled envelope is **361.920 mm long × 377.472 mm wide × 260.000 mm high**. I measured the union of precise bounding boxes of 1,914 visible BRep bodies, excluding hidden alternatives. Unmodeled cables, connectors and tether do not contribute to these dimensions.

All four horizontal U01 axes lie in the horizontal plane at **45° to the forward axis**. Both vertical U01 axes are perpendicular to that plane. The analytic shroud-cylinder radius is 31 mm.

## Reference and measurements

My geometric reference is the center of the visible assembly bounding box: CAD **(0.857, 0, 126.000) mm**. CAD axes are X forward, Y port, Z up. This point is not a measured center of mass.

The following axis points are relative to that reference, expressed as **forward, port, up** in millimeters:

- Rear starboard: **(-133.937, -128.920, -16.000)**.
- Rear port: **(-133.937, +128.920, -16.000)**.
- Front starboard: **(+130.509, -127.206, -16.000)**.
- Front port: **(+133.937, +130.634, -16.000)**.
- Vertical starboard: **(-3.281, -152.000, -14.472)**.
- Vertical port: **(+1.567, +152.000, -14.472)**.

I used a point on each analytic 31 mm-radius shroud-cylinder axis, not an inferred propeller center. For a force along that axis, moving the application point along the same line does not change its moment. The cylinder axis sign is a CAD convention; positive motor thrust and ArduSub channel mapping still require physical verification.

The [machine-readable snapshot](fusion-geometry.json) preserves the source occurrence paths, raw CAD axis points/directions, coordinate conversion and full precision. Unity coordinates are X starboard, Y up, Z forward, in meters.

## Printed parts and remaining calibration

All my 3D printed parts use **Bambu Lab PETG Basic at 100% infill**. The earlier 10% statement was corrected before recording this snapshot. This agrees with the existing 100% baseline print guide.

I have not substituted a CAD solid-mass estimate for the finished vehicle weight. Battery, wiring, hardware, printed-part slicing and actual water displacement still need to be accounted for. The simulator's 8 kg mass, buoyancy, drag and thrust values remain provisional. Its original actuator locations remain in place until the extracted geometry is integrated and the changed force moments are validated; this file records the calibration input without claiming that integration is complete.
