# Folder guide

I use `ClubROV` as the current working checkout for source and documentation. I kept the older OneDrive checkout as a backup; I avoid editing both copies in parallel so they do not drift apart.

- `Code/ROV ESP32-P4 code/`: current companion firmware, editable source, tests, dependency lock and local ignored credentials/build outputs.
- `Documentation/`: current setup, status, BOM, learning path and test-report template.
- `Hardware/`: current selected original-part exports and fit-study notes.
- `Models/`: historical complete assembly viewer.
- `Simulation/ClubROVSim/`: Unity Assets, Packages, ProjectSettings, source, documentation and packaging script.
- `Simulation/SITL/`: ArduSub setup, launchers, relay and validation tools.
- `Final Design/`: September frame/BOM baseline with explicit supersession notices.

I moved the original Animation, CameraBringup, MountRevision, TrayRevision and EndcapRevision folders to the parent workspace's `Archive/Working History 2026-10-23/`. The archive CSV records original/destination paths, sizes and SHA-256 hashes. Historical scripts may contain the old absolute working paths; preserve them as records rather than rerunning them without review. Credentials and logs in that archive are private and excluded from publication.

The OneDrive project's existing `Archive/Design History 2026-09-29/` and commercial reference library remain in place. I preserved the original files and verified the moved working-history files by size and SHA-256. The folder cleanup did not change the Fusion documents.

## Local generated files

Unity's `Library`, `Temp`, `Logs`, `UserSettings` and generated solution/project files are local Editor state, excluded from Git. Firmware `build-*`, `managed_components`, local `sdkconfig` and credentials are also ignored. Leave active caches and build directories in place while using the tools.

`Simulation/ClubROVSim/Builds/Windows/` is the active compiled player. The newest `Builds/Practice-*.zip` is the shareable snapshot; its timestamp records packaging time, not a promise that it matches unbuilt source changes. Keep the entire extracted player folder together. `Package-Practice.ps1` now writes directly to a ZIP instead of retaining another full extracted copy.

The 24 October cleanup moved older practice ZIPs, redundant extracted copies, a player startup log and Unity's `_Recovery` scene into the parent workspace's `Archive/Workspace Cleanup 2026-10-24/`. `MovedRoots.json` maps the old locations to their archived locations; `FileInventory.csv` records sizes and SHA-256 hashes. No source, CAD documents, firmware credentials, active build or Unity cache was deleted. The recovery scene is preserved outside Unity's active Assets tree.

The parent `.codex-remote-attachments` directory belongs to the chat application. The earlier historical archives, downloaded references and SITL installation remain outside the repository.
