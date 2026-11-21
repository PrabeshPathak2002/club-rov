# First pool course: runnable sensor policy

Double-click `../Run-Sensor-Course-Model.bat`. The launcher runs the simulator and opens a live forward-camera view at `http://127.0.0.1:8766/`. It repeats the course until **Stop model** is pressed or the terminal receives Ctrl+C. Opening the launcher again reuses an existing live preview.

Keep `SensorCourseGeometry.onnx` and `CourseMap.npz` together. The Python runner and the built sensor-training player are also required. Prepare the Ubuntu-22.04 WSL sensor environment with `bash setup-sensor-training.sh` from the parent directory, then build the sensor-training Windows player as described in the parent README. OpenCV must be available in that environment through `sudo apt-get install python3-opencv`.

The delivered policy combines a learned ONNX landmark estimator, a demonstration-learned map prior, camera marker measurements, and IMU/pressure/DVL feedback control. The map is registered against visually observed gates. Internal task progress comes from estimated crossings and holds; simulator scoring is used only for reporting episode outcomes. Inference does not read Unity target transforms, judge stage, or hold progress. The public course instructions supply heading objectives, with launch-yaw zero calibration.

## Verified scope

- Standard first-course layout, unseen start seeds 32000–32003: **39/40 full completions**, with all five objectives completed in successful runs. The remaining attempt reached the dock after completing four objectives.
- Initial independent full-course batch: **8/8**. The actual PowerShell launcher also completed a full course at seed 41000.
- Full-range randomized layouts: **0/20**. This learned-map policy currently supports the standard layout; randomized-course generalization remains unfinished.
- Simulation only; no real-vehicle validation.

The preview displays the policy's estimated task and the independently scored number of full completions. Run reports are stored beneath the WSL user's `~/club-rov-rl-results/live-sensor-course-*` folder. `manifest.json` identifies model, map and inference-runtime hashes used by the independent tests.

For a single fast terminal run from the `ReinforcementLearning` folder:

```powershell
.\Run-Sensor-Course-Model.ps1 -Episodes 1 -Speed 20 -NoPreview
```

Use the default speed of 1 for watching the camera. The earlier PPO and sensor-policy checkpoints remain separate and preserved.
