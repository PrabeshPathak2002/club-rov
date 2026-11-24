# Roadmap

This roadmap lists software improvements that new members can make without access to the vehicle. Each task starts from working code in this repository: take it, change it, measure the result against the current baseline and submit it. Changes are merged into `main` when they perform measurably better than the baseline or add a useful function that the project does not yet have.

Physical assembly, wiring and water testing are handled by the build leads and tracked in [STATUS.md](STATUS.md) and [TESTING.md](TESTING.md).

## How to contribute an improvement

1. Pick a task below and tell a project lead which one you are taking.
2. Set up the part you need:
   - Simulator: Unity 6000.6.7f1 with a personal Unity licence. See the [simulator guide](../Simulation/ClubROVSim/README.md).
   - ROS 2 navigation and missions: WSL Ubuntu 22.04 with ROS 2 Humble. See the [ROS 2 guide](../ROS2/README.md).
   - Course learning: the isolated Python environment. See the [course-learning guide](../ReinforcementLearning/README.md).
3. Run the baseline before changing anything and record the result.
4. Make your change on a separate branch or fork. Keep it small and focused on one task.
5. Run the same measurement again with the same seeds, episode count and settings. Run the existing tests for the area you changed.
6. Open a pull request using [CONTRIBUTING.md](../CONTRIBUTING.md). Include the before and after numbers, the commands you ran and anything that got worse.

A change counts as an improvement when it beats the baseline on the agreed measurement without breaking existing tests or making another measurement worse. A new function counts when it works, is documented, and includes a test or a short recorded demonstration.

## Current baselines

| Area | Measurement | Current result |
| --- | --- | --- |
| Sensor course policy, standard layout | Completed courses | 39 of 40 |
| Sensor course policy, randomised layout | Completed courses | 0 of 20 |
| PPO sensor-only policy | Full courses completed | 0 (hover and dock holds not learned) |

Measure course policies with `ReinforcementLearning/evaluate-course.py`; the [course-learning guide](../ReinforcementLearning/README.md) and [model notes](../ReinforcementLearning/Models/README.md) give the exact commands and settings. These are simulator results only.

## Starter tasks

Good first changes that take a few evenings and teach you how the code is organised.

- [ ] **Add a unit test** for a module that has none, such as `cerulean_dvl.py`, `sim_sensors.py` or `mapping_camera.py` in `ROS2/src/club_rov_navigation/club_rov_navigation/`, following the existing tests in `ROS2/src/club_rov_navigation/test/`.
- [ ] **Add a simulator map** in `Simulation/ClubROVSim/Assets/ROV/Scripts/RovTrainingMaps.cs` with a new obstacle or gate layout, and show it in the map selector.
- [ ] **Add a water-condition preset** (for example murky lake or clear pool) in `RovWaterVisibility.cs` and `RovPoolAppearance.cs`.
- [ ] **Improve the run review screen** in `RovRunReview.cs`, for example by showing time per course objective or path length.
- [ ] **Fix a setup guide** by following it on a fresh clone and correcting any step that fails or is unclear.

## Intermediate tasks

Changes that need an understanding of one subsystem and a clear before/after measurement.

- [ ] **Make marker detection more robust** in low visibility or at oblique angles (`marker_detector.py`). Measure detection rate on recorded prequalification footage with `prequal_review.py`.
- [ ] **Reduce navigation drift** by tuning `ROS2/src/club_rov_navigation/config/local_ekf.yaml` or the DVL processing in `dvl_delta.py`. Compare the estimated position with the simulator's true position over the same simulated run before and after; `ROS2/tools/check-simulation.py` currently only checks that a stationary estimate stays within 1 m, so adding a drift measurement is part of the task.
- [ ] **Add a mission safety check** in `mission_safety.py`, such as an abort when the estimated position leaves a set boundary, with a matching test.
- [ ] **Add a simulated sensor fault** (DVL dropout, depth noise or camera blackout) in `RovDvlSensor.cs` or `sim_sensors.py`, and show how the mission responds.
- [ ] **Speed up evaluation** in `evaluate-course.py` or the training player while producing identical results for the same seeds.

## Advanced challenges

Open problems where a working solution would be a significant result for the club.

- [ ] **Complete randomised courses.** The delivered policy relies on the standard layout and completes 0 of 20 randomised courses. Any improvement on that number is worth merging.
- [ ] **Learn the hover and dock holds from sensors.** The PPO sensor policy reaches the targets but does not hold position long enough to pass. See the hold criteria in the [course-learning guide](../ReinforcementLearning/README.md).
- [ ] **Hand control between ROS 2 and QGroundControl** with a clear command-ownership switch in simulation, so a pilot can always take over an autonomous run.
- [ ] **Run the prequalification mission end to end in simulation** with the ROS 2 stack, ArduSub SITL and the Unity simulator, and record the result.

Have an idea that is not listed? Propose it to a project lead with how you would measure it.
