# Folder guide

Paths below are relative to the repository root. The checkout directory can have any name. A Git clone contains the published source and reviewed assets; private archives, local credentials, installed tools and generated builds are separate.

## Repository contents

- `Code/ROV ESP32-P4 code/`: companion firmware source, host tests, dependency lockfile and credential template.
- `Documentation/`: architecture, setup, dated hardware status, component list, learning path and test-report template.
- `Hardware/`: current selected custom-part exports and fit-study notes.
- `Models/`: historical assembly-viewing mesh and preview image.
- `Simulation/ClubROVSim/`: Unity Assets, Packages, ProjectSettings, source, documentation and packaging script.
- `Simulation/SITL/`: ArduSub setup scripts, launchers, relay and validation tools.
- `ROS2/`: Humble sensor interfaces, estimator, external navigation, camera calibration, recording and mission exercises.
- `ReinforcementLearning/`: training/evaluation scripts, CPU budgeting, supplied policy/map assets and the fixed-course runner.

Use the [hardware revision index](../Hardware/README.md) to choose mechanical files. The assembly-viewing mesh is a visual reference and does not incorporate every later revision.

## Files generated during setup and use

Unity creates `Library`, `Temp`, `Logs`, `UserSettings` and IDE project files locally; they are excluded from Git. Firmware setup creates `build-*`, downloaded `managed_components`, local `sdkconfig` and the private credential header. Leave active caches and builds in place while the tools are using them.

A Windows build can be generated at `Simulation/ClubROVSim/Builds/Windows/`. The packager writes `Builds/Practice-*.zip` without retaining an extracted staging copy. A ZIP timestamp records packaging time, not source freshness; rebuild the player before packaging source changes. Keep all extracted player files together. See the [simulator build instructions](../Simulation/ClubROVSim/README.md#build-and-share).

SITL setup installs its source, Python environments and Linux build outputs under `~/club-rov-sitl` in Ubuntu 22.04. ROS 2 Humble can share that distribution while keeping its Python 3.10 packages and `~/club-rov-ros2-ws` build workspace separate from SITL's Python 3.12 virtual environment. Windows helper logs are written under `Simulation/SITL/logs/`. See the [SITL prerequisites](../Simulation/SITL/README.md#prerequisites). Neither installed tools nor generated outputs are supplied by a Git clone.

Superseded design documents, old screenshots and detailed development logs belong in a separate local archive outside this checkout. They are not part of the current build instructions.

Training environments, checkpoints, demonstration recordings and evaluation reports are generated under the WSL user's home directory. Only selected inference models and their provenance manifests are supplied in the source tree. The current sensor-course bundle lives in `ReinforcementLearning/Models`; Unity teacher/checkpoint assets serve separate demonstration and legacy-policy workflows. Their observation contracts differ.

Unity's locally embedded Inference Engine dependency is excluded. After initial package resolution, close Unity and run `Simulation/ClubROVSim/Tools/Prepare-InferencePackage.ps1` before player builds. Browser sessions, agent handoff notes, the unfinished TDR and machine-specific assistant preferences remain local.
