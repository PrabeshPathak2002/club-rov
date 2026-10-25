# Repository validation

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
