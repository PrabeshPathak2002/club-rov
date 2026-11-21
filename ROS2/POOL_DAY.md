# Pool day: first autonomous prequal runs

A checklist for running [the prequal mission](PREQUAL.md) on the physical ROV. Work through it in order; stop at any failed step. The first real runs are a test of the vehicle and the topside setup, not of the mission.

## Before leaving

- [ ] Battery charged (4S: 16.8 V full; the mission will not start below 14.0 V and surfaces below 13.2 V). Spare battery.
- [ ] Laptop: ROS workspace built (`bash ROS2/build-workspace.sh`, all tests pass), QGroundControl, this repo.
- [ ] Gate and marker colours known (or a printed sample), so you can check the detector hue ranges on real frames.
- [ ] Spotter and tether handler arranged. Agree the stop words and who presses Ctrl+C / disarms in QGC.

## ArduSub settings (once, then verify each time)

The mission refuses to start unless these are set. ArduSub's defaults only warn.

| Parameter | Value | Why |
|---|---|---|
| `FS_PILOT_INPUT` | 2 (disarm) | laptop crash or cut tether stops the thrusters |
| `FS_PILOT_TIMEOUT` | ≤ 3 s | |
| `FS_GCS_ENABLE` | 2 (disarm) or 4 (surface) | topside link lost |
| `FS_GCS_TIMEOUT` | ≤ 5 s | |
| `FS_LEAK_ENABLE` | 2 (surface) | leak sensor fitted and tested |
| `BATT_MONITOR` | configured | voltage must be reported |

Every failsafe ends with thrusters off, so **the vehicle must be slightly positively buoyant**: when disarmed in the water it should rise slowly by itself. The simulated vehicle is not; a disarmed real vehicle that sinks is the most likely way to lose it.

## Bench check (dry, propellers off or vehicle restrained)

- [ ] Ethernet: laptop at `192.168.137.1/24`, ESP32 at `192.168.137.2`. QGC shows SYSID, heartbeat, attitude and depth.
- [ ] Camera: the Pi Camera V2.1 streams 1632 × 1232 (`VIDEO.md`). The default `--hfov 48.7` assumes that full-width view behind a flat port; confirm the firmware isn't using a cropped mode, and [calibrate underwater](#calibrate-the-camera).
- [ ] Topside MAVLink: the ESP32 only accepts commands from `192.168.137.1:14550`, so one router owns that port and fans out, e.g. `mavproxy.py --master=udpin:0.0.0.0:14550 --out=udp:127.0.0.1:14551 --out=udp:127.0.0.1:14554`. Point QGC at 14551 (turn off its UDP 14550 auto-connect); the mission and recorder use `udpin:127.0.0.1:14554`. If the mission runs in WSL, verify these packets reach WSL.
- [ ] Topside video: one program can bind UDP 5600. For the mission use `--video "udpsrc port=5600 caps=application/x-rtp,media=video,encoding-name=H264,payload=96 ! rtph264depay ! h264parse ! avdec_h264 ! videoconvert ! video/x-raw,format=BGR ! appsink drop=true sync=false"` and turn QGC video off, or split the stream first. The mission's `--save-frames` gives you a record.
- [ ] Recorder smoke test: `ROS2/Record-Prequal-Footage.bat --video "<pipeline above>" --mavlink udpin:127.0.0.1:14554 --duration 30` saves frames with depth/heading.
- [ ] Mission precheck refusal: run the mission (below) with one failsafe deliberately wrong; it must refuse and name it. Put it back.
- [ ] Ctrl+C test with propellers off: start the mission, let it arm, press Ctrl+C — it must disarm.

## Calibrate the camera

Build the ROS workspace first. The tool uses OpenCV, NumPy and PyYAML (declared package dependencies); it never opens MAVLink or commands the vehicle. Print a checkerboard with **9 × 6 inner corners (10 × 7 squares)**, using 25 mm squares at 100% print scale. Check the square size with a ruler, laminate the print and mount it on a flat, rigid backing. Avoid glare and bending.

Practise on the bench in air, then repeat **underwater through the actual port**, with the mission's resolution, crop and fixed focus. Only the underwater result applies to the underwater mission. Keep air and water captures separate. In WSL:

```bash
bash ROS2/start-calibrate-camera.sh --video "<same source or GStreamer pipeline as the recorder>" \
  --board 9x6 --square 0.025 --medium water
```

Windows launcher: `ROS2/Calibrate-Camera.bat` with the same arguments. Without `--video`, the tool uses the recorder's ROS image topics. An existing video file, image folder, or recorder folder containing `frames/` can be passed with `--video` (use WSL paths). Add `--preview` when a graphical display is available; default capture is headless. Live capture stops after 180 s or 30 selected views; Ctrl+C or preview `q` fits the views already selected.

Move the board across the image, vary distance, and tilt it in both directions while keeping it completely visible. Automatic selection rejects blurred, small, edge-clipped and duplicate boards. Watch the 4 × 3 coverage display (`#` means seen), tilt spread and scale ratio. Too few views, poor coverage or nearly flat-on views fail without publishing a calibration; accepted images remain available for inspection.

The printed output directory contains accepted frames and matching `calibration.yaml` / `calibration.json`: resolution, fx/fy/cx/cy, OpenCV distortion coefficients, overall and per-view RMS error in pixels, medium and derived pinhole HFOV in degrees. Inspect corner detections and per-view errors; repeat captures with different views to check consistency. A small RMS alone does not prove accuracy through a refractive flat port, so validate against known targets at mission distances. The method uses [OpenCV camera calibration](https://docs.opencv.org/4.7.0/d9/d0c/group__calib3d.html).

Shared loader: `from club_rov_navigation.camera_model import load`; `model = load(path, size=(816, 616))` scales intrinsics for a full-frame resize and preserves dimensionless distortion and `model.medium`. It rejects aspect-ratio changes; same-aspect crops cannot be detected from resolution alone and require a new calibration.

Use it with `--calibration <dir>/calibration.yaml` on the mission, recorder and review tool. It replaces the `--hfov` estimate and removes lens distortion before detection. The mission only accepts an underwater (`medium: water`) calibration and reports the camera as unusable if the video's shape doesn't match the calibrated mode.

## At the pool, before any autonomy

- [ ] Leak check: vehicle in the water for 5 minutes, unpowered thrusters, inspect.
- [ ] Buoyancy: disarmed, it rises slowly. Trim if not.
- [ ] Manual dive in depth hold (QGC joystick): depth reading sensible, heading stable, no water in the enclosure.
- [ ] Record footage of the gate and marker from several distances (recorder, `--rate 2`). Review with `ROS2/Review-Prequal-Footage.bat` and tune `--hue` / `--gate-hue` until both are detected reliably and nothing else is. Use those values below.
- [ ] Measure the start heading: point the vehicle at the gate centre from the start position.

## Autonomous run

Disable the QGC joystick (QGC stays open to disarm). With the vehicle at the surface, pointed at the gate:

```bash
bash ROS2/start-prequal-hardware.sh --sysid <SYSID> --mavlink udpin:127.0.0.1:14554 \
  --video "<pipeline>" --calibration <underwater calibration.yaml> \
  --depth 1.0 --hue <low> <high> --gate-hue <low> <high> --save-frames ~/prequal-frames
```

Type `DIVE` at the prompt. Hardware defaults: depth limit = depth + 0.6 m (immediate surfacing), 300 s time limit, stick limit 0.6, battery start ≥ 14.0 V and surface below 13.2 V; override with `--max-depth`, `--time-limit`, `--max-stick`, `--min-voltage`, `--abort-voltage`. It refuses to start unless the vehicle is at the surface (< 0.3 m), disarmed, the failsafes above are set, and depth, heading and camera are live.

Stopping: **Ctrl+C** stops the thrusters and disarms; `ros2 topic pub --once /mission/cancel std_msgs/msg/Empty` surfaces under control; changing mode in QGC hands control back to the pilot; disarming in QGC always works.

Suggested progression: shallow depth and short time limit first (`--depth 0.8 --time-limit 60`), then the full run. Keep each report (`~/club-rov-ros2-ws/reports/prequal-*.json`) and the saved frames.
