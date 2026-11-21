# Agent guide

Instructions for AI coding assistants (Codex, Cursor and others) working in this repository. Several assistants take turns on the same checkout, so continuity lives in files, not chat history.

## Start and finish every session

1. Read `HANDOFF.md` in this folder when present (local, git-ignored). It records the current goal, what is done, what is unfinished and the next steps.
2. Check `git status` and `git log --oneline -10` before editing.
3. Before finishing, or when usage is about to run out, update `HANDOFF.md`: what changed, what is half-done, exact next steps, and any commands still running (trainers, SITL, relays). Keep it current rather than appending a journal.
4. Do not commit or push unless the user asks. This repository is public; much of the working tree is deliberately unpublished.

## Project map

- `Code/ROV ESP32-P4 code/`: companion firmware (ESP-IDF).
- `Simulation/ClubROVSim/`: Unity 6000.6.7f1 pool simulator (URP, Input System, ML-Agents 4.2.0). Detailed engineering context: `Simulation/ClubROVSim/Docs/AI/UnityProjectContext.md`.
- `Simulation/SITL/`: ArduSub Sub-4.7.2 SITL in WSL Ubuntu-22.04 with Windows launchers.
- `ROS2/`: ROS 2 Humble DVL simulation, local odometry and external navigation.
- `ReinforcementLearning/`: PPO training of the first pool course with ML-Agents (`README.md` there).
- `Documentation/`, `Hardware/`, `Models/`: guides, BOM, mechanical exports. `Documentation/FOLDER_GUIDE.md` explains generated outputs.

## Environment

- Windows host with WSL `Ubuntu-22.04`. Run Linux tooling through `wsl -d Ubuntu-22.04 -- bash -lc '...'`; prefer single quotes in PowerShell or a script file, because PowerShell does not escape `\"`.
- Python environments are separate: SITL uses Python 3.12 under `~/club-rov-sitl`, ROS 2 uses system Python 3.10, RL uses `~/club-rov-rl-venv` (Python 3.10, mlagents 1.1.0, CPU torch 2.2.2). RL results go to `~/club-rov-rl-results/<run-id>`.
- The Unity Editor is usually open on `PoolTraining`. Do not hand-edit scene YAML; use the Editor scripts under `Assets/ROV/Editor`.

## Conventions

- Follow `CONTRIBUTING.md`: neutral documentation voice, explicit units, distinguish planned, implemented, simulated and water-tested behavior, record failures as well as successes.
- Unity units are metres, +Z forward, +X starboard, +Y up. Local physics runs in `FixedUpdate`; SITL mode owns physics exclusively. Never drive both paths at once.
- Use `RovDynamics.PhysicsStepped` as the simulation clock, not wall-clock `Update`.
- Include Unity `.meta` files with assets. Keep `Library`, `Temp`, `Logs`, `UserSettings`, builds, check reports and RL results out of Git.
- No credentials, personal paths or raw session logs in tracked files.
