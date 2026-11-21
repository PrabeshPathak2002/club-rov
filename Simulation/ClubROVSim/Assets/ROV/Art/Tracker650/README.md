# Tracker 650 visual source

`Tracker650Exterior.json` contains a tessellated exterior from Cerulean's [Tracker 650 V2 STEP model](https://drive.google.com/file/d/1Lb65fHTB4j13HLF-Y7GYLb3JkIc2r5mk/view), plus the local underside adapter and nominal mounting hardware from the Future ROV fit study.

Coordinates are millimetres in the Fusion assembly frame: X forward, Y port, Z up. The simulator reference is `[0.8569993, 0, 126]` mm. `RovDvlVisuals` converts these coordinates to metres and reflects triangle winding into Unity's coordinate convention. Acoustic-beam reference cones and internal piezo/foam parts are omitted.

The installer generates four mesh/material groups under `Assets/ROV/Generated/Tracker650` and attaches them beneath the vehicle as `Tracker 650 DVL visual`. Use **ROV -> ROS2 -> Install DVL visual** outside Play Mode to rebuild the visual. The navigation-sensor installer includes this step too.

This is a visual representation of the provisional CAD mounting. It adds no rigid body or colliders and does not revise the simulator's mass, buoyancy or inertia. The measurement origin in `RovDvlSensor` remains a separate provisional calibration value. Rendering a DVL does not provide ArduSub with navigation measurements; see the [ROS 2 guide](../../../../../../ROS2/README.md).
