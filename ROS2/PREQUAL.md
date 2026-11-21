# RoboSub prequalification mission

Autonomous attempt at the RoboSub prequalification course: start 3 m behind a 2 m gate whose bar is 1 m deep, pass under it, circle a floor-to-surface marker 10 m beyond, and return under the gate.

The mission uses only what the real vehicle will carry: pressure depth, the ArduSub compass heading and the forward camera. It does not use the DVL, the ROS position estimate or simulator truth. ArduSub stays in **ALT_HOLD** (depth hold) and the mission sends joystick-style `MANUAL_CONTROL`, which is how a tethered pilot would drive it.

## Run it in the simulator

1. In Unity, choose **ROV → Reinforcement Learning → Return to manual piloting** if RL was enabled, open `PoolTraining` and enter Play Mode.
2. Run `Simulation/SITL/Start-ClubRovSitl-Prequal.bat`. With the Unity CLI available it selects **Gate & pillar**, paints the marker orange, connects SITL, starts the ROS sensor stream and the FPV camera feed. Without the CLI, do those steps by hand (map before **CONNECT SITL**).
3. Keep the QGC joystick disabled, then run `ROS2/Run-Prequal-Mission.bat`.

Progress is logged as `PREQUAL <phase>` lines and published as JSON on `/mission/status`; publish `std_msgs/Empty` to `/mission/cancel` to abort. Each run writes `~/club-rov-ros2-ws/reports/prequal-*.json` with phase events and twice-a-second samples of depth, heading and detections. Unity's course panel judges the run independently (gate pass, 360° around the pillar, return pass).

Restart the whole launcher between runs: reconnecting SITL inside one session resets the simulation clock, which the ROS sensor pipeline rejects.

`Start-ClubRovSitl.ps1 -ConnectUnityEditor -Prequal -CeruleanDvl` runs the same session with a simulated Tracker 650 feeding ArduSub's EKF directly (`VISION_POSITION_DELTA`, as the real DVL does); the mission does not use it yet.

## Run it on the vehicle

`ROS2/start-prequal-hardware.sh` (`--hardware`): typed confirmation, refuses to start unless ArduSub's failsafes disarm/surface, the battery is charged and the vehicle is disarmed at the surface; surfaces at a depth limit or low battery; caps the stick. Follow [POOL_DAY.md](POOL_DAY.md).

## Behaviour

| Phase | What it does |
| --- | --- |
| DIVE | Holds launch heading and steps to 1.6 m (below the bar). |
| OUTBOUND | Cruises. While both gate posts are visible it aims at the gate centre; afterwards it holds that course until the marker appears, then homes on it. |
| ORBIT | At about 2.6 m from the marker: strafes sideways while yawing to keep the marker centred and holding about 1.2 m by its apparent width. Ends after a full 360° of line of sight. |
| ALIGN | Stops circling and strafes back onto the original approach line (averaged over the last 3 s of marker sightings before the circle) and holds it for 2 s, so leftover sideways drift does not carry the return leg past a gate post. |
| TURN / RETURN | Turns to the reverse of the approach line and cruises back, re-aiming at the gate centre whenever both posts are visible. The leg ends 6 s after the gate was last seen close; if the gate is never seen, after 1.2× the outbound time. |
| SURFACE | Climbs and disarms. |

Aborts (depth over 3 m, marker not found within 90 s, marker lost for 8 s while circling, camera loss for 3 s, phase timeouts, 480 s mission limit) surface in depth hold. If depth or heading telemetry is stale for 2 s, the mission climbs blind with full up stick for up to 40 s, then disarms. (ArduSub's SURFACE mode did not climb in SITL, so it is not used.)

Depth corrections step past ArduSub's stick deadzone (about ±25%) and are neutral within 0.1 m, so depth hold does the holding.

## Detection

`marker_detector.py` thresholds the 320×180 camera image in HSV. The marker is the largest tall, thin blob in hue 0–15 (red-orange). The gate is found as two posts: column runs of gate colour (hue 17–60, saturation ≥ 40) at least 20% of the image tall, of similar height and ending on a common bottom line, neither touching the image sides; anything estimated beyond 12 m is ignored. Posts are paired column by column rather than as one connected outline because water haze turns the far side of the gold gate green-grey (hue ~45–50, saturation ~50) and breaks it away from the bar. This rejects bar fragments, solid blobs and lone posts, which otherwise steered the return leg off course. In SITL the gate is only visible within about 4 m; from farther the return leg relies on the approach line and compass. The gold gate is deliberately outside the marker range — otherwise its posts look like markers near the gate.

For the real course, tune `--hue LOW HIGH` (marker) and `--gate-hue LOW HIGH` from real footage (next section); underwater colour shifts toward blue-green with distance. Other options: `--depth`, `--cruise`, `--radius`, `--return-scale`, `--time-limit`, and `--save-frames DIR` (one camera frame per second while armed, named by time, phase and whether the marker/gate was detected).

## Recording real-pool footage

Before any autonomous run in a new pool, drive the course by hand while recording, then tune the colours from the frames.

1. Start the recorder: `ROS2/Record-Prequal-Footage.bat` with the camera and telemetry sources, e.g. the RTP pipeline and MAVLink router in [POOL_DAY.md](POOL_DAY.md). `--video` takes anything OpenCV opens (device index, file, RTSP, or a GStreamer pipeline containing `!`); without it the recorder reads the ROS topics `--image-topic`/`--info-topic`. `--mavlink none` records camera only.
2. The recorder is **read-only**: it never sends MAVLink, so it cannot arm, change modes or move the vehicle. Drive with the QGC joystick as usual. It saves `--rate` frames per second (default 2) as JPEG plus `log.jsonl` with depth (ArduSub's calibrated depth from VFR_HUD), heading, raw internal/external pressure, armed state and the current detections. Recordings go to `reports/recording-<UTC>` in the ROS workspace (`\\wsl$\Ubuntu-22.04\root\club-rov-ros2-ws\reports\` from Windows).
3. Drive past the gate and the marker from several distances (2–15 m), at mission depth, facing toward and away from windows and lights.
4. Review: `ROS2/Review-Prequal-Footage.bat <recording folder> [--hue LOW HIGH] [--gate-hue LOW HIGH] [--saturation N] [--gate-saturation N] [--marker-diameter M] [--gate-width M]`. It prints per-frame detections, estimated distances and the median water colour, and writes annotated copies to `<folder>/review/` (marker colour tinted red, gate colour yellow, detected marker as a line, gate as a box). Adjust until the marker and gate are found at the distances you need and nothing else is — lane lines, flags, ladders and swimmers' legs are the usual false matches. Then pass the same `--hue`/`--gate-hue` to the mission.

Frames larger than 320 px wide are shrunk to 320 px for detection, so the pixel thresholds behave the same as in simulation. Without `camera_info`, the focal length comes from `--hfov`.

**Camera (current vehicle): Raspberry Pi Camera V2.1 (IMX219) behind a flat port**, streamed by the ESP32-P4 at 1632 × 1232 (full sensor width, 2×2 binned). Its 62.2° horizontal view in air narrows to about 48.7° underwater (refraction at the flat port), which is the `--hfov` default. Distance estimates scale with this, so the marker approach and orbit radius are only as good as it. Notes:
- A cropped sensor mode narrows the view and makes 48.7° wrong.
- The V2.1 has a fixed-focus lens (factory-set to roughly infinity), so nothing hunts; focus is adequate beyond about 1 m.
- A dome port would keep the in-air value.
- An underwater checkerboard calibration would replace the estimate with a measured focal length and lens distortion.

## Tests

`test/test_prequal_plan.py` covers the detectors on synthetic images and flies the full mission in a planar kinematic model with camera field of view, visibility limits, compass noise, the depth-hold deadzone and off-centre or mis-aimed starts. It checks both gate passes are within 0.8 m of centre and below the bar, and that the path winds once around the marker. `test/test_prequal_footage.py` covers downscaling of large camera frames, the field-of-view focal length and the review tool.

## Limits

- Simulation only: the node requires loopback SYSID 42 SITL (Sub-4.7.2, `SIM_GPS1_ENABLE=0`). A hardware build needs a separate guard, kill-switch integration and in-water tuning.
- Approach distance and orbit radius rely on the marker's assumed 0.16 m diameter and the camera intrinsics from `/mapping/rgb/camera_info`.
- Water currents are not compensated beyond what the camera re-aiming corrects.
