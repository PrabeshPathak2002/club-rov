# Folder guide

`ClubROV` is the active source and documentation checkout. The older OneDrive checkout is retained as a backup; editing both copies in parallel risks divergent versions.

- `Code/ROV ESP32-P4 code/`: current companion firmware, editable source, tests, dependency lock and local ignored credentials/build outputs.
- `Documentation/`: current setup, status, BOM, learning path and test-report template.
- `Hardware/`: current selected original-part exports and fit-study notes.
- `Models/`: historical complete assembly viewer.
- `Simulation/ClubROVSim/`: Unity Assets, Packages, ProjectSettings, source, documentation and packaging script.
- `Simulation/SITL/`: ArduSub setup, launchers, relay and validation tools.
- `Final Design/`: September frame/BOM baseline with explicit supersession notices.

The original Animation, CameraBringup, MountRevision, TrayRevision and EndcapRevision folders were moved to the parent workspace's `Archive/Working History 2026-10-23/`. The archive CSV records original/destination paths, sizes and SHA-256 hashes. Historical scripts may contain the old absolute working paths; preserve them as records rather than rerunning them without review. Credentials and logs in that archive are private and excluded from publication.

The OneDrive project's existing `Archive/Design History 2026-09-29/` and commercial reference library remain in place. The original files were preserved, and moved working-history files were verified by size and SHA-256. The folder cleanup did not change the Fusion documents.

## Local generated files

Unity's `Library`, `Temp`, `Logs`, `UserSettings` and generated solution/project files are local Editor state, excluded from Git. Firmware `build-*`, `managed_components`, local `sdkconfig` and credentials are also ignored. Leave active caches and build directories in place while using the tools.

`Simulation/ClubROVSim/Builds/Windows/` is the active compiled player. The newest `Builds/Practice-*.zip` is the shareable snapshot; its timestamp records packaging time, not a promise that it matches unbuilt source changes. Keep the entire extracted player folder together. `Package-Practice.ps1` now writes directly to a ZIP instead of retaining another full extracted copy.

The 24 October cleanup moved older practice ZIPs, redundant extracted copies, a player startup log and Unity's `_Recovery` scene into the parent workspace's `Archive/Workspace Cleanup 2026-10-24/`. `MovedRoots.json` maps the old locations to their archived locations; `FileInventory.csv` records sizes and SHA-256 hashes. No source, CAD documents, firmware credentials, active build or Unity cache was deleted. The recovery scene is preserved outside Unity's active Assets tree.

The parent `.codex-remote-attachments` directory belongs to the chat application. The earlier historical archives, downloaded references and SITL installation remain outside the repository.

## Archive retention

Obsolete simulator package copies from the 24 October cleanup were subsequently deleted after checking their inventory and confirming that the active Windows build and current ZIP remained available. `DeletedGeneratedRoots.csv` and `DeletedGeneratedFiles.csv` in the private cleanup archive record the removed files; the original move inventory remains a historical record, not a list of files still available for restoration.

The archived `Animation/assembly-mesh.json` is stored as `assembly-mesh.json.gz` to preserve its contents with less disk usage. `CompressedFiles.csv` records the original and compressed hashes and sizes. Its decompressed SHA-256 was verified before removing the uncompressed copy. Decompress it to the original filename before using historical scripts that expect JSON. Unique CAD history, bench logs, the recovery scene, private configuration and the installed QGroundControl files are retained.
