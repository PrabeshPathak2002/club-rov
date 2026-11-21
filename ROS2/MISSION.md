# Autonomous simulation exercise

This optional ROS 2 node commands the simulated Club ROV to dive, travel forward, turn 180 degrees, return to its launch position and surface. It uses `/odometry/local` for horizontal position and heading, and simulated ArduSub pressure for depth. Unity ground truth is not an input to the mission controller.

## Run

1. Build the ROS workspace with `bash build-workspace.sh` in Ubuntu 22.04.
2. In Unity, disconnect SITL, select **Empty pool**, set current to zero and buoyancy to **+1%**. These are test conditions, not measured vehicle properties.
3. Start `Simulation/SITL/Start-ClubRovSitl-ExternalNav.bat`. Wait for healthy DVL navigation and a disarmed vehicle.
4. Disable the QGroundControl joystick so pilot commands do not compete with the mission.
5. Run `ROS2/Run-Sim-Mission.bat`. This explicitly arms the simulated vehicle after its checks pass.

The default exercise travels 4 m at 1.8 m depth. From a sourced Ubuntu environment, `bash start-sim-mission.sh --distance 3 --depth 1.5` changes those goals. Distance is limited to 1–6 m and depth to 0.6–2.3 m. Check pool clearance before starting.

The mission runs only through a fixed loopback endpoint and checks SYSID 42, submarine firmware 4.7.2 and the expected simulation/navigation parameters. It is a simulation exercise, not a hardware mission runner. Starting the normal sensor stack does not start a mission.

## Monitoring and stopping

`/mission/status` publishes JSON with phase, reason, simulated time, depth, armed state and navigation validity. Reports are written outside Git under `~/club-rov-ros2-ws/reports/mission-*.json`. A successful run ends disarmed and returns to Manual mode. Re-enable the QGC joystick after testing.

To request recovery, publish one `std_msgs/msg/Empty` message on `/mission/cancel` in ROS domain 42. Ctrl+C requests an immediate stop and disarm; always verify the vehicle is disarmed before closing the simulation.

Invalid DVL/navigation stops horizontal progress and requests Depth Hold. Recovery within two simulation seconds allows the original goal to resume after a healthy interval. Longer loss requests Surface mode. A surface recovery timeout disarms. Sensor-session changes, clock faults and stale depth telemetry also stop the exercise. Depth and horizontal geofence limits bound the mission.

## Limits

Arrival requires position, heading, depth and speed to remain within tolerances for 1.5 simulation seconds. This is a relative navigation demonstration, not SLAM, obstacle avoidance or a RoboSub task planner. Long DVL outages can leave the current estimator with excessive position covariance; reacquiring velocity does not restore absolute position. Restart the sensor/estimator session after such a failure.

The hydrodynamics, buoyancy and sensor noise remain provisional. Neutral buoyancy did not maintain passive surface flotation after disarming in the initial test; positive buoyancy is therefore an explicit test condition. Mission reports contain estimator values. Independent Unity measurements are needed to assess actual return error and surfacing.

## Initial validation

On 30 October 2026, a 4 m route at 1.8 m depth completed with +1% buoyancy, no current, a 0.15 m/s horizontal command limit and a 0.30 rad/s yaw-rate limit. Independent Unity samples nearest mission start and completion showed approximately **0.082 m horizontal return error** and **0.118 m center depth** at completion. The vehicle remained near the surface after disarming. A short navigation interruption during surfacing recovered and resumed the goal.

A separate run with deliberately injected 100% DVL dropout entered navigation hold, requested Surface after the two-second loss limit, and ended disarmed with `aborted_surfaced`. Both reports are local runtime artifacts. Earlier, faster runs also encountered navigation loss; these results do not establish repeatability across currents, sensor noise, hardware or all pool conditions. The ROS package suite passed 25 tests, including seven mission geometry and command-limit cases.
