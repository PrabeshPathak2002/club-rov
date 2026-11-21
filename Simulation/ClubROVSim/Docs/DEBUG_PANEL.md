# Debug menu

Select **DEBUG** in the left sidebar. The scrollable drawer refreshes at 5 Hz while visible.

- Control owner, arm/telemetry state, mode, render FPS and SITL timing/step counters.
- Latest simulated IMU gyro (rad/s), specific force (m/s²), launch-relative depth (m), variances, sequence, simulation timestamp, sample age and stream/session counters.
- Latest actual DVL sample: bottom-lock/dropout reason, validity, beam count, altitude, FLU velocity and variance. IMU-only packets do not overwrite it. Invalid velocity is unavailable rather than a misleading zero.
- ROS launch-relative horizontal position and yaw from the existing DVL-track return channel. Old or stopped sessions are labeled waiting/stale; simulator position is never substituted for an estimator reading.
- Separately labeled simulator truth: world position, velocity, angular velocity, depth, heading, rotation, applied thruster percentages/health, water current/density, buoyancy and tether payout/tension/force/anchor.

FLU means forward/left/up. Truth vectors explicitly use Unity world XYZ. No magnetometer, camera image analysis, battery, temperature or leak measurements are invented; those sensor models are not implemented.

Opening the drawer does not start a connection. **START ROS SENSOR STREAM · LOOPBACK** starts the existing simulated measurement stream; no ROS receiver is required to inspect the samples. Stopping/restarting interrupts external navigation and starts a new session, so the button is disabled while SITL is armed or its arm state is unknown. The panel does not launch ROS or SITL, change physics ownership, arm the vehicle or change sensor noise. Sensor observations cache the same generated packet values without extra random draws or sampling.

Validation: Unity compilation passed; 18 DVL checks passed, including emitted-value agreement, preservation of the last DVL sample across IMU-only packets and session-reset clearing. The 32 menu/view checks passed, including exclusive DEBUG selection and disable/re-enable without duplicates. Native mouse streaming and live display were inspected. Final interactive validation follows a clean Play restart because dynamic Pipeline evaluations can disable UI input in this Editor version. No standalone player build was made.
