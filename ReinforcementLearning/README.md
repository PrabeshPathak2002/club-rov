# Autonomous course experiments

This directory contains a delivered fixed-layout sensor policy and separate ML-Agents training experiments. The objective is three gates in order, a three-second hover, and a three-second dock hold. Course completion uses the existing course evaluator. The legacy PPO policy uses privileged simulator observations. The sensor-only workflow and delivered hybrid policy use distinct observation contracts and evaluation results.

## Run the delivered first-course policy

Double-click `Run-Sensor-Course-Model.bat` for the learned-map and sensor-feedback policy, with a live camera preview and independently scored completions. It runs the standard first-course layout. See [the model notes](Models/README.md) for exact validation results and limitations, including the unsuccessful randomized-layout test. The delivered hybrid policy is separate from the PPO experiments below.

## Start training

1. Open PoolTraining in Unity and stop Play. Disconnect SITL and stop the external navigation/mapping launcher.
2. Run `Setup-Course-Training.bat` to create the isolated Python environment in Ubuntu-22.04.
3. In Unity choose ROV → Reinforcement Learning → Enable first-course training.
4. Run Start-Course-Training.bat, wait for “Listening on port 5004,” then press Play in Unity.
5. Let training run. Stop the trainer with Ctrl+C to save a checkpoint. Stop Unity Play afterwards.

The default run targets ten million decision steps. This can take hours on a single Editor environment. Training uses CPU PyTorch and a target simulation speed of 20× with an uncapped frame rate. The achieved speed depends on the laptop. The trainer reports reward and step count, and saves checkpoints under ~/club-rov-rl-results/<run-id> in WSL.

To resume, run the batch with --resume --run-id <existing-run-id>. To monitor learning, run ~/club-rov-rl-venv/bin/tensorboard --logdir ~/club-rov-rl-results --host 127.0.0.1 in WSL and open localhost:6006.

## Faster parallel training

Several headless copies of the simulator train faster than one Editor session.

1. Stop Play in Unity. Choose ROV → Reinforcement Learning → Build parallel training player. This writes Simulation/ClubROVSim/Builds/Training/ClubROVSim.exe; rebuild it after changing simulator or agent code.
2. Run Start-Parallel-Training.bat. It starts eight players without graphics; set CLUB_ROV_RL_NUM_ENVS in WSL to change the count. Add --resume --run-id <existing-run-id> to continue a run, including one started in the Editor.
3. Stop with Ctrl+C in the trainer window. The players close with the trainer.

The players enter training mode only when mlagents-learn launches them. On a 20-thread i9-12900H laptop, eight players reached about 1,000 decision steps per second, roughly ten times the Editor at 10× time scale.

Before building a Windows player with ML-Agents 4.2.0, open the project once to download its packages, then close Unity and run `pwsh -File ../Simulation/ClubROVSim/Tools/Prepare-InferencePackage.ps1` from this directory. Reopen Unity afterwards. The script embeds the pinned Inference Engine package locally and restricts its ONNX importer Protobuf DLL to the Editor, preventing a duplicate player dependency. The downloaded package stays outside Git. The registry dependency in the published lock file supports initial package resolution.

## Evaluate a trained policy

Copy the exported PoolCourse.onnx from the run directory into the Unity project's Assets folder. Stop Play, select that imported model in the Project window, choose ROV → Reinforcement Learning → Run selected trained policy, then press Play. Evaluation runs the same first-course environment with episode resets.

To return to manual piloting, choose ROV → Reinforcement Learning → Return to manual piloting. This stops Play. Start Play again for normal controls.

## Environment

- Four continuous actions command local translation XYZ and yaw through the existing mixer, capped at 80% thrust.
- Twenty-six scalar observations include target-relative position, local linear/angular velocity, world-up in body coordinates, target heading error, six applied thruster outputs, objective stage and hold progress.
- The target for gate approach lies 0.7 m beyond the gate plane so crossing is rewarded. Rewards include distance progress, completed objectives and ready-to-hold time. Collisions and timeouts incur penalties.
- Episodes randomise the launch pose and course layout according to the curriculum, and terminate on completion, a 150-second timeout, surfacing or leaving the pool bounds.
- Training selects map 0 with zero current, neutral buoyancy and the default neutral 100 ft tether. It uses the chase camera and suppresses manual input, sensor streaming, HUD, audio and scored history recording during the Play session. Stop Play to restore the scene.
- The legacy `PoolCourse` policy observes simulator state and known course targets. The separate sensor policy below uses camera and simulated sensor packets. Testing a trained policy on different conditions is needed before assessing robustness.

## Layout generalisation

The PPO configuration targets ten million decision steps. Use the supplied launchers: `course-trainer.py` adapts ML-Agents 1.1.0 curriculum evaluation to `Course/Completed`, without changing PPO's shaped rewards. Each lesson needs at least 200 fresh episode outcomes and completion above 90%. Randomisation progresses through 0%, 25%, 50%, 75% and 100% of the configured ranges. `Course/Objectives` reports the number of objectives completed at each episode end; `Course/RandomisationScale` records difficulty.

At full difficulty the start varies by ±1.5 m laterally, ±0.3 m vertically and ±30° yaw. Each gate/hover/dock varies by up to ±1 m laterally, ±0.4 m vertically, ±0.75 m longitudinally and ±15° yaw. Sampling respects pool clearance; gate ordering and opening dimensions remain intact. Disabling RL restores the saved target transforms. A zero randomisation scale preserves the previous fixed course and its small start jitter.

Parallel training limits numerical-library threads and Unity job workers. A Windows job shared by all parallel training players caps their combined CPU at 60% of the machine, leaving headroom for Python and other applications. The launcher builds the small Framework 4.8 host into its generated WSL wrapper directory; no executable is stored in the repository. `cpu-budget.py` samples Windows total CPU roughly every two seconds (including the Windows query), pauses its own trainer process group at 75% and resumes at 60%. Players then wait for trainer actions. Sampling provides headroom below the 90% budget but cannot prevent brief spikes from other applications. CPU samples are saved alongside results in a `*-cpu.jsonl` file. Choose `CLUB_ROV_RL_NUM_ENVS` according to measured throughput and available CPU headroom. The recorded generalisation run used 18 workers.

`evaluate-course.py --policy <ONNX> --profile fixed|randomised --output <JSON> --episodes 100 --workers 4` runs deterministic held-out evaluations against the rebuilt player. It requires `onnxruntime` in the isolated RL environment. Use the same seed and episode count for comparisons; fixed means the original small start jitter. Training success is measured separately from evaluation success.

## Validation

ML-Agents 4.2.0 compiled in Unity 6000.6.7f1 and connected to Python mlagents 1.1.0 through communicator API 1.5.0. The isolated environment uses Python 3.10.12, CPU PyTorch 2.2.2 and NumPy 1.23.5.

The 2026-11-07 generalisation change passed clean Unity compilation, 2,000 sampled layout checks (ranges, pool clearance, gate ordering, scales and restoration), a Play-mode agent-disable/manual-input check, and a Windows training-player build with zero errors or warnings. Seven Python regression tests passed. A 60,000-step trainer smoke test verified completion-driven curriculum advancement with real player statistics.

The selected checkpoint is `pool-course-generalise-stable-20261107-0920/PoolCourse/PoolCourse-3629544.onnx`, under `~/club-rov-rl-results/` in WSL. It descends from the overnight `pool-course-20261107-011312` policy through a preserved 999,919-step randomisation checkpoint, followed by 3,629,544 fine-tuning steps at learning rate 0.0001. The initial 0.0003 run regressed after reaching full ranges; its final model was not selected. Both training runs used 18 environments; their recorded total CPU peaks were 89%.

Fresh deterministic evaluation used the rebuilt Windows player and seeds 9500/9501, with the same 200 randomised layouts per policy. The overnight policy completed 110/200 (55%); the selected policy completed 188/200 (94%). Both completed 100/100 fixed-course episodes. The new policy averaged 4.78 objectives; its twelve failures occurred at Gate 2 (ten), Gate 3 (one) and dock (one). This demonstrates substantial layout generalisation with remaining failures. The result was one percentage point below the proposed 95% early-stop target; training was left stopped rather than spending the entire 10M-step ceiling. Remaining gate approaches are the next targeted improvement.

Evaluation reports are `~/club-rov-rl-results/evaluation/final-9500-{old,new}-{fixed,randomised}.json`, with model SHA256 values and per-worker outcomes. The selected model SHA256 is `fe6323290bc1c32f3c9e39d82c3c604aba649b20d670aca45f16545feaaf8e39`. The existing imported Unity ONNX asset was not replaced; the evaluated export remains in WSL.

## Sensor-only full-course policy

`PoolCourseSensors` is a separate, incompatible policy. Its actor receives a body-mounted RGB camera (84×64, 48.7° horizontal field of view) and 16 normalized values: accelerometer (3), gyro (3), pressure depth (1), DVL body-centre velocity (3), altitude (1), DVL validity/beams/age (3), IMU age (1), and IMU validity (1). Native Python images are CHW (3,64,84). Missing/stale readings are masked. DVL lever-arm correction uses measured angular velocity. Sensor sampling uses the existing noisy sensor model without starting UDP/ROS streaming. Pressure uses the simulated water surface at y=0.

Command history is excluded to avoid an imitation-learning shortcut that repeats the preceding command. The actor receives no exact pose, objective position, judge stage, hold progress or applied thruster outputs. Simulator truth still supplies episode resets and training rewards. The CNN has recurrent memory so it can infer motion and course progress from the observation history. This is simulator training; real-vehicle inference and water validation are separate work.

1. With the shared Editor free and outside Play, choose **ROV → Reinforcement Learning → Build sensor-only training player**. It writes `Builds/SensorTraining/`, preserving the legacy player/model.
2. In WSL, run `bash ReinforcementLearning/setup-sensor-training.sh`. It creates a separate CUDA environment and preserves the CPU RL environment.
3. Run `bash ReinforcementLearning/collect-sensor-demonstrations.sh` from the repository. It records 100 randomised expert episodes with four rendered players. The preserved legacy policy supplies training-only action labels; each recording contains the student's camera/sensor inputs. Set `CLUB_ROV_RL_SENSOR_DEMOS` to a new empty output directory to change the destination. Collection converts the recordings into its `aligned/` subfolder; use that folder for training. Inspect each worker's `teacher-summary.json`; these are expert results, not student evaluation. Do not mix failed infrastructure smoke recordings into the dataset.
4. Set `CLUB_ROV_RL_SENSOR_DEMOS` to the `aligned/` directory, then use `Start-Sensor-Training.bat` or `bash ReinforcementLearning/start-sensor-training.sh`. Default: four rendered environments and GPU PPO with behavioural cloning, decaying over the first 500,000 steps. `CLUB_ROV_RL_RUN_ID`, `CLUB_ROV_RL_NUM_ENVS` and optional `CLUB_ROV_RL_MAX_STEPS` select the run, worker count and step ceiling. Pass `--resume` with the same run ID to resume; Ctrl+C saves and closes players.
5. Run `sensor-preflight.py` with the sensor Python environment to audit finite sensor values, valid readings, changing camera frames and absence of the teacher. Evaluate exported checkpoints with `evaluate-course.py --sensors --policy <immutable.onnx> --profile fixed|randomised --episodes 100 --workers 4 --require-objectives --output <report.json>`. Recurrent state resets at episode boundaries. Keep held-out seeds separate from training.

The sensor launcher requires rendering and must not use `--no-graphics`. The same shared Windows CPU cap and total-CPU guard apply. Results and run-local configuration are stored under `~/club-rov-rl-results/`. The teacher is instantiated only with `--rov-sensor-demo`; normal training/evaluation rejects an active teacher. Demonstration generation calls the teacher synchronously so each action label corresponds to the recorded sensor observation, without a second agent's decision delay.

For a faster recording-only warm-start, `pretrain-sensors.py --demos <directory> [--checkpoint <sensor checkpoint.pt>] --output <new results run directory> --updates 1000` uses the standard ML-Agents behavioural-cloning module without simulation rollouts. It validates the sensor observation dimensions, loads actor weights strictly, exports an actor checkpoint/ONNX and writes losses plus source/dataset hashes to `cloning-report.json`. Its update count is not an RL environment-step count, and its loss is not course performance. Evaluate the export before using `--initialize-from=<new run ID>` for PPO; the critic starts fresh. Omitting the checkpoint creates a fresh matching actor and initializes vector normalization from the recorded sensor values.

ML-Agents 4.2 records the action that led to each observation; the Python 1.1 demonstration loader expects the action chosen from that observation. `align-sensor-demos.py --input <raw directory> --output <new aligned directory>` shifts action labels within each episode, removes the four command-history fields from older 20-value recordings, clears terminal labels and drops an incomplete tail. For old 20-value recordings it verifies raw labels against the previous-command inputs; new 16-value recordings require the raw action-semantics manifest from the collector. It refuses already-aligned input and writes source hashes plus episode counts to `alignment.json`. Training launchers require that manifest. Using unconverted data teaches a previous-command copy shortcut; low cloning loss then gives a misleading result. Raw recordings are retained for audit.


Sensor imitation uses `sensor_bc.SensorBCModule` to convert recorded commands into PPO's raw action units. In ML-Agents 1.1, the non-tanh actor executes `clip(raw,-3,3)/3`; the stock BC loss compares raw samples directly with demonstration commands in [-1,1]. The sensor adapter scales supervised continuous targets by three, both offline and during PPO-assisted cloning. Recordings retain physical command units. `pretrain-sensors.py --repair-legacy-action-scale` supports a one-time conversion of old BC action-head weights with a legacy source report; it rejects a second conversion of a corrected checkpoint. Diagnostic evaluation `--action-gain 3` is recorded in reports and is not the default deployment path.

The sensor PPO actor preserves the input-normalization statistics saved by its imitation warm-start. Actor weights continue learning and critic normalization remains adaptive. This prevents the sensor encoding from drifting as early PPO trajectories visit a different state distribution. The legacy PoolCourse trainer is unchanged.

## Hover and docking practice

Set `CLUB_ROV_RL_HOLD_PRACTICE=1` for a new sensor-training run. The generated configuration mixes episode starts: 30% full course, 50% cyan hover, 20% green dock. Practice starts approach from 2 m away with horizontal/depth/heading jitter of 0.3 m / 0.1 m / 10 degrees and a 30-second timeout. Reset-only geometry keeps the target within the body-camera frustum and the vehicle inside the pool; it faces the target bearing and reduces jitter when needed. This gives the recurrent actor an approach history before the marker leaves its forward view. Environment parameters control these ranges. Defaults in the player are zero practice probability, so normal evaluation always starts the full course unless explicitly configured otherwise.

Practice resets are unscored and skip only the already-completed objectives. They do not change the original hold position, heading, speed or three-second duration checks. Full-course rewards are retained; during holding, bounded heading/low-speed potential differences provide guidance without a repeating stationary reward. Completing a practice goal receives a terminal bonus. These changes affect training rewards/resets only: the actor still receives RGB plus the same16 sensor values, including pressure depth and DVL altitude. No downward camera, exact target position, judge stage or hold progress is added to observations.

`Practice/HoverCompleted` and `Practice/DockCompleted` report practice success. `Course/Completed` and `Course/Objectives` are emitted only for full-course episodes, so shortcuts do not inflate course completion or advance its curriculum. BC assistance lasts3Msteps in this mode. Use a new run ID; do not resume the old run that regressed after BC decay. `sensor-preflight.py --practice hover|dock` validates the practice reset, sensor contract and statistic separation at the exact target pose; it does not evaluate a learned policy.

Add `--approach-distance 2` to the practice preflight to audit visible approaches instead; zero commands are not expected to complete these probes. `evaluate-course.py --sensors --practice hover|dock --approach-distance 2` measures learned practice performance separately from full-course completion. `prepare_hold_demos.py` retains successful full trajectories and adds aligned hold clips; the sensor BC loss masks sequence padding. Offline `pretrain-sensors.py --sequence-length 256` can preserve longer approach/hold context without changing the exported actor's observation or recurrent-memory dimensions. Imitation loss alone is not evidence of course success.

For failed approaches, use the evaluator's `--capture <new-folder>` to save actor camera frames and sensor/command traces. Compare a normal run against the diagnostic `--action-gain .25` using the same policy and seeds. `diagnose-sensor-imitation.py --policy <model.onnx> --demos <aligned-successful-folder> --episodes 12 --output <report.json>` separately measures command error on expert observation histories. Its optional `--reset-at-hold-approaches` discards recurrent memory at objective transitions to isolate memory dependence. Rewards classify report phases only; they never enter inference. Replay accuracy and reduced-command tests are diagnostic evidence, not full-course completion results.

`collect-sensor-corrections.py` enables the separate `--rov-sensor-corrections` player mode. It records feedback-expert labels on current RGB/sensor observations while executing a short student prefix followed by expert recovery. Collection labels travel through statistics channels, never actor inputs. Terminal observations have zero next-action labels; failed student rollouts remain useful corrective examples. The collection-only hold expert uses simulator position/velocity/heading feedback. Normal demonstrations retain the preserved learned teacher, and normal student evaluation requires teacher absence.

Dock approach practice starts 0.5 m above the docking depth and includes a lateral offset to keep the marker visible near the pool wall. This permits entry above the platform rail; same-depth starts outside the rail can trap the vehicle. The feedback expert approaches above the platform before descending. Manual courses and full-course starts retain their existing layouts and judge criteria. `merge-sensor-corrections.py` combines already-aligned full-course examples with weighted feedback recordings without shifting their labels again. Validate the collection expert first, then compare the trained student with its baseline on the same rebuilt player and held-out seeds before a long PPO run.

For consistent full-course examples, collect with `--practice full --student-steps 0`: the preserved learned teacher handles gates and the feedback teacher handles holds. This avoids mixing different hold-control styles in the supervised dataset. The merger's `--base-copies` and `--copies` control the relative weights of full courses and corrective examples. Expert recovery rates must be reported separately from student evaluation rates.

For an independent visible trainer window, set `CLUB_ROV_RL_RUN_ID`, `CLUB_ROV_RL_SENSOR_DEMOS`, `CLUB_ROV_RL_HOLD_PRACTICE=1` and run `start-visible-sensor-training.sh`. It writes the console log while showing it, and keeps the trainer independent of a separate log-viewer tab. Keep the trainer window open. Ctrl+C or termination requests a saved shutdown. `CLUB_ROV_RL_AUTO_RESUME=1` resumes a saved checkpoint automatically; an interrupted run without its first checkpoint needs a new run ID.

Sensor runs now save every25,000steps and retain20checkpoints. Preserve selected evaluated models separately before they age out. Explicit `CLUB_ROV_RL_MAX_PERFORMANCE=1` lifts the shared player CPU cap to100% and disables total-CPU pausing while retaining monitoring; defaults retain the earlier headroom limits. Worker count is controlled separately. More players should be selected by measured steps/second, since camera and policy processing can limit throughput before CPU/GPU utilization reaches100%.
