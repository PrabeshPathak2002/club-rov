# Repository validation

## Source publication review - 21 November 2026

- ROS 2 Humble rebuilt `club_rov_interfaces` and `club_rov_navigation`, with 82 tests passing.
- Course-learning tooling passed 35 Python tests. The selected model, learned map and two inference-runtime files match their recorded SHA-256 hashes in the staged Git content.
- Firmware host tests passed 110,979 frame-delivery cases and 168 heartbeat cases.
- PowerShell files parsed, 26 Bash scripts passed syntax checks, and the local Inference Engine preparation script passed first-run and repeated-run checks.
- The staged source passed local documentation-file link checks and credential/personal-path pattern checks. Unity assets have matching metadata and no duplicate GUIDs. Generated timers, logs, local dependencies and the unfinished TDR are excluded.
- A clean Unity 6000.6.7f1 batch import exited with code 198 before compilation because no active Editor license was available. Fresh Unity compilation, player builds and runtime behavior were not verified during this publication review. Previous dated simulator results remain recorded separately.

## Recorded source checks — 23–24 October 2026

- A fresh ESP-IDF **5.5.2** configuration and full companion build passed with the committed dependency lockfile. The tested target used the pre-v3 P4 revision setting, 32 MB flash, IMX219 format index 2, 100 Hz scheduler and custom OTA layout.
- Host MAVLink framing checks recorded **110,979 exact frame deliveries**; heartbeat parsing checks recorded **168 passing cases**.
- The [.github workflow](../.github/workflows/host-checks.yml) runs the portable host tests on pushes and pull requests. Hosted ESP-IDF builds are not configured.
- Unity/SITL checks, Windows builds and remaining simulator limits are summarized in [simulator validation](../Simulation/ClubROVSim/Docs/VALIDATION.md).
- Physical bench observations and outstanding vehicle tests are recorded in [hardware status](STATUS.md). Software and CAD checks do not establish pressure capacity or field reliability.

## Publication checks

The reviewed source set excludes populated credential headers, deployed firmware binaries, complete local `sdkconfig`, private serial logs, downloaded dependency/build directories and private archives. Images and configuration used by the Unity scene remain tracked; generated reports and superseded diagnostic captures are ignored.

The practice packager was checked for ZIP integrity, agreement with the compiled player and exclusion of debug/backup output. Packaging copies an existing player; source changes require a new build before distribution.

Local documentation links, heading anchors and text whitespace were checked during the documentation review. Fenced commands and parameter blocks were preserved. These checks are publication checks, not new firmware flashes, player builds or physical tests.

## Retained development history

Detailed per-run reports, old screenshots, the superseded September CAD inventory and workspace cleanup inventories are retained locally by the maintainer. The current repository contains the build inputs, selected hardware files, active documentation and concise validation summaries. The [folder guide](FOLDER_GUIDE.md) distinguishes tracked inputs from generated outputs.

The repository cleanup removed redundant material from the current branch without rewriting Git history. Previously committed versions remain recoverable from their commits.
