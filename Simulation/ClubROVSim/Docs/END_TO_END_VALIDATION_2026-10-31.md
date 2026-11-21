# Simulator end-to-end validation — 31 October 2026

Validated on Z16P-PP with Unity 6000.6.7f1 and Ubuntu-22.04 WSL. UTC mission timestamps fall on 1 November. All autonomous runs used the guarded loopback SYSID42 simulator; no hardware was tested.

## Results

- ROS workspace: 26 tests passed.
- Unity: 168 functional checks passed across course progression, six map layouts, guidance, run review, tether, DVL, DVL track map, SITL protocol and timing. One legacy synthetic HUD restart check failed during the broken UI-input session; it remains a recorded failure, not a passing automated check.
- Active profile: SCHED_LOOP_RATE=200, MAV_SYSID=42, FRAME_CONFIG=1, VISO_TYPE=1.
- Disarmed pacing with sensor streaming and tether: 10.149 simulation seconds / 10.114 wall seconds = 1.003x, at 118.8 render FPS.
- External-navigation hold: maximum horizontal excursion 0.030 m over eight simulation seconds. Pilot reposition traveled 0.231 m and settled to 0.010 m/s. Ended disarmed.
- Four-metre autonomous mission: completed and disarmed. Empty pool, zero current, +1% buoyancy and enabled 30.48 m tether. Independent Unity trace measured 0.108 m horizontal return error, 0.122 m final armed depth and 0.688 N maximum tether force.
- Intentional total DVL loss: NAV_HOLD at simulation time 35.495 s, ABORT_SURFACE at 37.595 s, DISARM at 41.595 s. Outcome `aborted_surfaced`, disarmed, Manual. Unity depth varied 0.00715 m during the 2.1 s NAV_HOLD interval. The mission process returned its expected non-success status for an aborted mission.

## Menu-input investigation and recovery

Both the user and native mouse automation reproduced unresponsive menus. UI geometry and hit testing were correct; native mouse devices were enabled. During the failure, UI Toolkit's internal play-mode flag was false while Application.isPlaying was true, and the InputForUI provider was enabled but uninitialized.

A clean Play stop/start restored native input. A minimal Pipeline command, `script eval` evaluating only `return UnityEngine.Application.isPlaying;`, reproduced the failure: the previously working drawer's Close button stopped responding. Another clean Play stop/start restored input again. This isolates the observed trigger to dynamic evaluation in this Editor/tooling combination. The exact internal static-reset mechanism has not been established.

After the final clean restart, native mouse actions successfully performed COURSE → End practice & review → Close → Restart course. The review displayed its route and thruster metrics; restart restored Gate 1, zero elapsed time, zero collisions and zero completed objectives. No speculative EventSystem component or private-engine reflection workaround was persisted.

For future validation, run dynamic C# evaluations before the final Play restart. Do not evaluate C# during a session intended for mouse interaction; if input stops after an evaluation, stop and restart Play. Prefer registered commands and native mouse interaction for final UI verification. This is a validated recovery/workflow restriction, not a permanent upstream tooling fix.

## Evidence and cleanup

Local, ignored evidence is under `Logs/`: `EndToEndChecks.txt`, `EndToEndSummary.json`, `FullMissionReport.json`, `FullMissionTrace.csv`, `DropoutReport.json` and `DropoutTrace.csv`. Original mission reports were `mission-20261101T031523Z.json` and `mission-20261101T031728Z.json` in the WSL ROS workspace reports directory.

SITL/ROS launch sessions were stopped after disarm. Runtime test settings were restored and Play mode stopped, discarding the temporary EventSystem experiment. The final compilation status reported no failure; Editor console ground truth reported zero errors and zero warnings. Captured tool history retained five warnings, so this is not a claim that the entire investigation was warning-free. Existing unrelated working-tree changes were preserved.

Limits: one successful autonomous mission and one long-loss trial; no repeatability qualification, full human-piloted course, standalone player build, hardware controller qualification or real DVL/tether calibration. Course checks use synthetic poses and do not establish physical pilotability.
