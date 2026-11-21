# ROS 2 navigation experiments

ROS 2 Humble on Ubuntu 22.04 receives simulated DVL, raw IMU and depth measurements from the Unity ROV. An optional Madgwick attitude filter and `robot_localization` EKF produce continuous local odometry for navigation and mapping experiments.

An optional loopback-only MAVLink bridge sends this estimate to the pinned ArduSub SITL vehicle, enabling QGC position telemetry and Position Hold. ArduSub remains the actuator controller. An optional RTAB-Map mode builds a 3D map from simulated RGB-D and the same local odometry.

## Data flow

Unity `RovDvlSensor` sends loopback UDP on port 9010. The Windows relay forwards newline-delimited packets to the ROS receiver over loopback TCP 9011, using WSL's localhost forwarding. ROS publishes the simulation clock and sensor messages; the local estimator consumes those messages.

- `/dvl/data` (`club_rov_interfaces/msg/Dvl`): timestamped sensor-frame velocity, covariance, beam count, approximate altitude, validity and reason.
- `/dvl/velocity_body` (`geometry_msgs/msg/TwistWithCovarianceStamped`): valid velocity measurements only, corrected from the sensor location to the vehicle origin using angular velocity and the mounting offset.
- `/imu/data_raw` (`sensor_msgs/msg/Imu`): angular velocity and specific force. Orientation is unavailable and its covariance starts with -1. Acceleration includes gravity.
- `/imu/data` (`sensor_msgs/msg/Imu`): attitude-filter output when the estimator is enabled. No magnetometer or north reference is supplied.
- `/depth/pose` (`geometry_msgs/msg/PoseWithCovarianceStamped`): only Z is a measured quantity; unobserved pose axes have large covariance and are excluded by the EKF configuration.
- `/odometry/local` (`nav_msgs/msg/Odometry`): estimated position, full orientation, velocity and covariance, with TF `odom -> base_link`.
- `/diagnostics`: sensor freshness, DVL validity, session and rejected-packet details, plus estimator diagnostics.
- `/clock`: simulation time, which can advance slower than wall time.

The bridge also publishes static transforms `base_link -> imu_link` and `base_link -> dvl_link`. The local estimator is the only publisher of `odom -> base_link`. A future mapping system can own `map -> odom`; no fixed map correction is assumed here.

Ground truth is disabled by default on both sides. If explicitly enabled in the Unity Inspector and ROS launch, it appears only on `/simulation/ground_truth/odometry`. No estimator input references that topic, and truth does not publish TF.

## Coordinates and launch origin

Distances are metres, angular velocity is radians per second, and acceleration is metres per second squared. ROS body axes are forward/left/up (FLU). The fixed `odom` frame uses the launch heading for +X, launch-left for +Y and up for +Z. Its zero height is the starting vehicle position, not the water surface. Yaw is positive counterclockwise viewed from above.

The simulated IMU is at the vehicle reference origin. The virtual DVL axes are aligned with the body FLU axes; its provisional offset is `[-0.000857, 0, -0.18]` m, informed by the underside CAD placement and the simulator's geometric reference. This is not a surveyed acoustic origin, measured COM, or verified Tracker factory-axis calibration. Before physical integration, measure the mounting transform and verify the vendor velocity-axis convention. Changing the simulator offset requires the matching ROS `dvl_offset` parameter; mismatched packets are rejected.

Reset the vehicle, choose the map and set the start pose **before** starting the sensor stream. Starting the stream creates its launch origin. A reset or stream restart creates a new session; restart the ROS launch as well. The bridge rejects a different session until restarted so it cannot silently splice two missions into one map. Gaps and reconnects within one session preserve the origin. Duplicate and out-of-order samples are rejected.

## Set up Humble

Use Ubuntu 22.04 with Python 3.10. For Windows, install/register a WSL2 distribution named `Ubuntu-22.04`. Both the ROS and SITL launchers target this distribution. SITL retains its own Python 3.12 virtual environment, while ROS uses the system Python 3.10. The `.bat` files target `Ubuntu-22.04`; native Linux users run the shell/Python commands directly.

From this directory in Ubuntu:

```bash
sudo bash setup-humble.sh
bash build-workspace.sh
```

The setup script checks the OS and installs official ROS repositories, Humble base, message/build tools, Madgwick and `robot_localization`. It updates packages in the selected environment. The build script keeps generated files under `~/club-rov-ros2-ws`, links `src` to this checkout and runs package tests. Set `CLUB_ROV_ROS_WS` to choose a different build workspace. No compiled ROS installation or WSL distribution is included in Git.

## Run with the Unity Editor

1. Open `Simulation/ClubROVSim/Assets/Scenes/PoolTraining.unity`. The scene includes `RovDvlSensor` and a CAD-derived DVL/adapter visual beneath the vehicle. For an older scene, use **ROV -> ROS2 -> Install navigation sensor stream** outside Play Mode; this installs both the component and visual. **Install DVL visual** rebuilds just the rendering assets.
2. Select the map/start pose, enter Play Mode and let the vehicle settle. For a stationary validation, keep local controls neutral and current off.
3. Start **Start-ROS2-Sensors.bat**. This launches the bridge, attitude filter and local EKF in WSL. Alternatively run `bash start-ros2.sh` in Ubuntu. ROS domain 42 and localhost-only discovery keep the development graph local.
4. Start **Start-Unity-Sensor-Relay.bat** on Windows. It requires Python 3 on PATH. Alternatively run `python tools/relay-unity-sensors.py`. It does not buffer old data while disconnected.
5. In Unity choose **ROV -> ROS2 -> Start sensor stream (Play Mode)**. Use **Stop sensor stream** to stop publishing. Keep Unity running in the background if another application has focus.

For sensor topics without the estimator, pass `with_estimator:=false` to the ROS launcher. To inspect truth, enable `Include Ground Truth` on the Unity component and pass `publish_ground_truth:=true` to ROS. Both opt-ins are required.

Inspect data from another Ubuntu terminal:

```bash
source /opt/ros/humble/setup.bash
source ~/club-rov-ros2-ws/install/setup.bash
export ROS_DOMAIN_ID=42 ROS_LOCALHOST_ONLY=1
ros2 topic echo /dvl/data
ros2 topic echo /odometry/local
ros2 run tf2_ros tf2_echo odom base_link
```

Run one inspection command at a time or use separate terminals. Record experiments with `ros2 bag record /dvl/data /dvl/velocity_body /imu/data_raw /imu/data /depth/pose /odometry/local /diagnostics /tf /tf_static /clock`, writing the bag outside this source tree.

Before resetting, changing maps, recompiling or restarting the stream, stop the ROS launch with Ctrl+C. Restart it after the reset to establish a fresh estimator session. Stop the Windows relay with Ctrl+C when finished. A stopped stream reports stale status using a wall-clock watchdog even when `/clock` stops. No replacement zero-velocity observation is generated on DVL loss.

## Live 3D mapping with RTAB-Map

Install `ros-humble-rtabmap-ros` in the same Ubuntu 22.04 distribution and rebuild the workspace. The viewer uses WSLg on Windows.

1. Select a map and launch pose in Unity before streaming.
2. Use **Start-ROS2-Mapping.bat** in place of the normal ROS sensor launcher. In Ubuntu the equivalent is `bash start-ros2.sh with_mapping:=true`. Run one ROS sensor launch per session.
3. Run **Start-Unity-Sensor-Relay.bat** and start the Unity sensor stream under **DEBUG**. An already-running relay reconnects when the ROS launcher restarts.
4. In Unity open **CONTROLS → START RGB-D MAPPING**. This selects FPV. Keep FPV active and pilot through the pool to capture new viewpoints. The RTAB-Map window displays the incoming images and accumulated cloud.

Mapping capture pauses outside FPV. Use **STOP RGB-D MAPPING** to release the capture resources. Restart the ROS launch after a vehicle reset or sensor-session change. RTAB-Map saves each launch to a new `~/club-rov-map-*.db` by default. Use `mapping_database:=/path/to/map.db` to choose a file, or `mapping_viewer:=false` for a headless run.

The capture sends 320×180 color and simulated optical-axis depth up to 30 Hz through separate loopback TCP 9013. Capture skips frames when the previous GPU readback or send is still in flight. RTAB-Map targets 3 map updates per simulated second. It uses the rendered scene depth within 12 m and samples color before pilot video delay. ROS publishes matching timestamps on `/mapping/rgb/image`, `/mapping/depth/image` and `/mapping/rgb/camera_info`. RTAB-Map uses the local EKF transform `odom -> base_link` and the fixed camera mount, then owns `map -> odom`. Keep the estimator enabled. Mapping is off by default and uses a simulated depth camera. The physical ROV would need a suitable depth or stereo sensor for this same pipeline.

## Measurement model and limits

The DVL samples velocity at its offset using translation plus `omega cross r`. Three ideal rays tilted 20 degrees from downward test for floor-like returns. All three must pass the configurable range and tilt checks. Self-colliders and trigger volumes are excluded. Overfull ray-hit buffers invalidate the return. Defaults are 0.5-25 m approximate altitude and 20 degrees tilt, with configurable Gaussian velocity noise and random dropout. These are development assumptions, not a calibrated model of Tracker 650 accuracy, acoustic beam shape or pool reverberation.

IMU sampling is capped by the physics-step rate; its configured target is 100 Hz. DVL target is 10 Hz. Local practice and SITL can have different actual sample rates. Measurements use simulation timestamps, not arrival time. Noise is seeded, covariance reflects configured white noise, and gyro noise is included in the lever-arm correction's conservative velocity variance. Bias drift, acoustic latency, multipath and real hardware timestamp synchronization remain to be modeled or measured.

The initial EKF fuses DVL velocity, relative depth, attitude-filter roll/pitch and gyro rates. It does not integrate linear acceleration during outages. Yaw starts relative to launch and drifts; there is no absolute heading, loop closure or field map. IMU-derived attitude and gyro measurements are correlated, so this initial configuration and its reported covariance require validation and tuning before any hardware-control use. On bottom-lock loss, the filter can continue predicting but its position is not newly observed. Consumers must use diagnostics and covariance rather than assuming odometry publication means a valid navigation solution.

Do not feed ArduSub fused pose back as independent evidence into this estimator. The optional external-navigation path below consumes only the independent ROS sensor estimate.

## DVL-assisted ArduSub SITL and QGC

One-time setup, from `ROS2` in Ubuntu 22.04 after the Humble and SITL setup steps:

```bash
bash setup-external-nav.sh
bash build-workspace.sh
```

The optional sender uses pinned `pymavlink==2.4.50` in `~/club-rov-ros2-ws/mavlink-venv`, with access to system ROS Python 3.10. This is separate from SITL's Python 3.12 environment.

1. Enter Unity Play Mode with `PoolTraining` loaded. Keep the vehicle stationary and disarmed during navigation initialization.
2. Run [Start-ClubRovSitl-ExternalNav.bat](../Simulation/SITL/Start-ClubRovSitl-ExternalNav.bat). It starts SITL, the ROS sensors/estimator/odometry sender, both Windows relays and the Unity sensor stream. Do not also start the individual ROS/relay launchers.
3. Connect QGC to **TCP 127.0.0.1:5765**, SYSID **42**. Wait for the EKF to initialize and position telemetry to become available, then select **Position Hold** and arm. Neutral sticks hold position; forward/lateral input repositions the hold target. QGC still provides joystick input and arming.
4. Disarm before stopping. Stop the launcher with Enter, then disconnect SITL in Unity. Restart the entire session after resetting the vehicle or changing maps.

The combined launcher uses `run-external-nav` for its EEPROM and logs under `~/club-rov-sitl`. The normal launcher retains `run` and its original navigation settings. Source defaults do not overwrite parameters already saved in an EEPROM. To rebuild a modified navigation profile, stop it first and move its `eeprom.bin` to a backup before restarting.

For manually managed processes, run the SITL PowerShell launcher with `-ExternalNavigation`, start the Unity ROS2 stream after connecting, and let that launcher manage ROS and the sensor relay. `Start-ROS2-ExternalNav.bat` remains available for separately managed Linux/SITL sessions; never run two receivers on the same ports.

The sender checks loopback UDP **14553**, SYSID 42, submarine type, Sub-4.7.2 and simulation/source parameters before sending estimates or setting the simulated EKF origin. It sends MAVLink 2 `ODOMETRY` in `LOCAL_FRD`/`BODY_FRD`. Launch-relative FLU position is rotated onto the captured ArduSub heading while stationary; body velocity and angular rates are converted to forward/right/down. No truth pose is used. Sensor/estimator time resets require a coordinated restart.

The pinned firmware uses **`VISO_TYPE=1`** for the MAVLink backend. `external-nav.parm` selects ExternalNav for horizontal position/velocity, pressure for vertical position and compass for yaw. ROS yaw still drifts; the initial heading alignment does not provide continuous compass correction to the ROS estimator. The MAVLink sender does not arm, change modes, write parameters or command motors.

The EKF origin uses the existing SITL home coordinate **-35.363261, 149.165230**, at 0 m datum. QGC therefore displays a synthetic geographic location around that coordinate, not the real pool location or a GPS measurement. ROS `odom` remains relative to the launch point. This does not create a field map or SLAM solution.

Invalid DVL bottom lock, stale sensor/odometry data, excessive covariance or a reset stops forwarding odometry. A publishing EKF alone is not evidence of valid bottom lock. ArduSub then applies its own EKF timeout and failsafe behavior; this integration is not a validated autonomous recovery policy. Disarm/recover in the simulator before restarting a failed session. Provisional noise, hydrodynamics and mounting calibration must be replaced or measured before hardware use.

## Validation

- Both packages built with ROS 2 Humble on Ubuntu 22.04.
- Fifteen Python tests cover valid/invalid measurements, nonfinite inputs, mounting mismatch, lever-arm correction, time/session rejection and fragmented/oversize TCP input.
- Fifteen Unity checks cover FLU polar/axial signs, quaternion yaw, rotational lever arm, clock ordering, floor lock, minimum altitude, tilt, dropout, reset and socket cleanup.
- A live Windows Unity -> relay -> WSL Humble -> Madgwick/EKF check received DVL and IMU samples, finite normalized odometry and healthy sensor diagnostics without ground-truth input. An eight-second wall-time stationary observation ended about 5 mm forward, 19 mm left and 6 mm below the launch origin. It demonstrates integration, not navigation accuracy or long-term drift performance.
- Stopping the Unity stream produced stale/invalid sensor diagnostics with zero rejected packets and no replacement DVL or IMU samples. The watchdog uses wall time even with a paused simulation clock.
- After migrating SITL into Ubuntu 22.04, a combined ArduSub/ROS run passed the stationary sensor/local-odometry check while ArduSub owned Unity physics. The separate guarded motor test passed and ended disarmed. ROS estimates were not sent back into ArduSub.
- The optional external-navigation integration subsequently passed on Sub-4.7.2: QGC's TCP endpoint received global/local position, Position Hold was accepted, and an eight-simulation-second hold in a 0.2 m/s cross-current had 0.012 m maximum horizontal excursion. A pilot reposition traveled 0.247 m and settled to 0.005 m/s horizontal speed. The guarded check ended disarmed. These are short simulation integration checks, not measured DVL accuracy.
- Injected 100% DVL dropout withheld odometry; the sent-message counter remained unchanged across three consecutive status reports. Eighteen Python tests pass, including FRD conversion and covariance rejection.

Reproduce the package tests with `bash build-workspace.sh`. In Unity Play Mode, `ClubROV.Simulation.Editor.RovDvlChecks.Run()` runs isolated sensor checks. With a neutral, stationary ROV and the live stack active, run `python3 tools/check-simulation.py` from a sourced Humble terminal. The live check assumes the vehicle is stationary; it is not a general mission acceptance test.

## Source references

- [Humble Ubuntu installation](https://github.com/ros2/ros2_documentation/blob/humble/source/Installation/Ubuntu-Install-Debs.rst)
- [ROS IMU message contract](https://github.com/ros2/common_interfaces/blob/humble/sensor_msgs/msg/Imu.msg)
- [ROS odometry message contract](https://github.com/ros2/common_interfaces/blob/humble/nav_msgs/msg/Odometry.msg)
- [robot_localization Humble configuration](https://github.com/cra-ros-pkg/robot_localization/blob/humble-devel/params/ekf.yaml)
- [ArduPilot external-navigation interface](https://ardupilot.org/dev/docs/mavlink-nongps-position-estimation.html)

## Unity DVL track minimap

The top-right **DVL TRACK** map receives `/odometry/local` from the ROS estimator. The sensor node returns compact telemetry over the existing TCP 9011 connection; the Windows relay forwards it to Unity loopback UDP 9012. The normal combined launcher starts this path automatically. No simulator pose or ArduSub fused position is used to draw the map.

The arrow uses ROS estimated yaw, and the trail uses estimated horizontal position. Up is launch-forward, left is launch-port; the yellow cross marks the launch origin. This is a local trajectory display, not a geographic map, pool outline or SLAM result. The grid auto-fits the retained trail. It retains at most 512 points, sampled when motion exceeds 0.05 m, and redraws at 10 Hz.

Invalid bottom lock, stale measurements, high covariance or a stalled odometry clock pauses the trail and grays the last valid arrow. Reacquisition starts a new segment without drawing a line across the gap. A new Unity sensor session clears old mission history and rejects packets from other sessions. **Clear Trail** clears the drawing only; it does not reset ROS position. Click the map header to collapse it; H hides it with the main HUD.

## Autonomous simulation exercise

The optional [mission exercise](MISSION.md) dives, travels a launch-relative route, turns, returns and surfaces using ROS odometry and ArduSub Guided control. It starts only through Run-Sim-Mission.bat, explicitly arms the isolated simulator, and requires the QGC joystick to be disabled. See the guide for recovery behavior and current limitations.

## RoboSub prequalification run

The [prequal mission](PREQUAL.md) passes under the gate, circles the marker and returns using only pressure depth, ArduSub heading and the forward camera — no DVL or position estimate — with a separate hardware runner for future integration tests. Physical mission performance remains unvalidated.

## Publication verification - 21 November 2026

Both Humble packages rebuilt successfully and all 82 navigation tests passed, including camera calibration, prequalification planning, mission safety, framing and sensor relay cases. These are software checks. Physical navigation, the current endcap assembly and water performance remain unvalidated.
