# Folder guide

Paths below are relative to the repository root. The checkout directory can have any name. A Git clone contains the published source and reviewed assets; private archives, local credentials, installed tools and generated builds are separate.

## Repository contents

- `Code/ROV ESP32-P4 code/`: companion firmware source, host tests, dependency lockfile and credential template.
- `Documentation/`: architecture, setup, dated hardware status, component list, learning path and test-report template.
- `Hardware/`: current selected custom-part exports and fit-study notes.
- `Models/`: historical assembly-viewing mesh and preview image.
- `Simulation/ClubROVSim/`: Unity Assets, Packages, ProjectSettings, source, documentation and packaging script.
- `Simulation/SITL/`: ArduSub setup scripts, launchers, relay and validation tools.
- `Final Design/`: September frame/BOM documentation with supersession notices. Historical CAD/print packages named there are not distributed in this repository.

Use the [hardware revision index](../Hardware/README.md) to choose mechanical files. The September frame notes and assembly preview do not incorporate every later revision.

## Files generated during setup and use

Unity creates `Library`, `Temp`, `Logs`, `UserSettings` and IDE project files locally; they are excluded from Git. Firmware setup creates `build-*`, downloaded `managed_components`, local `sdkconfig` and the private credential header. Leave active caches and builds in place while the tools are using them.

A Windows build can be generated at `Simulation/ClubROVSim/Builds/Windows/`. The packager writes `Builds/Practice-*.zip` without retaining an extracted staging copy. A ZIP timestamp records packaging time, not source freshness; rebuild the player before packaging source changes. Keep all extracted player files together. See the [simulator build instructions](../Simulation/ClubROVSim/README.md#build-and-share).

SITL setup installs its source, Python environments and Linux build outputs under `~/club-rov-sitl` in Ubuntu. Windows helper logs are written under `Simulation/SITL/logs/`. See the [SITL prerequisites](../Simulation/SITL/README.md#prerequisites). Neither installed tools nor generated outputs are supplied by a Git clone.

## Private maintainer archives

The following records describe the original development workspace. They are optional maintainer history, not directories that readers must recreate:

- `Archive/Working History 2026-10-23/`: camera bring-up, Animation, MountRevision, TrayRevision and EndcapRevision work, with original/destination paths, sizes and SHA-256 inventories. Private credentials and raw logs are excluded from publication.
- `Archive/Workspace Cleanup 2026-10-24/`: preserved Unity recovery scene and startup log, original move inventories and subsequent deletion manifests. Obsolete generated practice packages were deleted after inventory verification; the original inventory alone does not identify which files remain restorable.
- The earlier OneDrive checkout and `Archive/Design History 2026-09-29/`: historical source models, design scripts and commercial reference geometry retained outside this repository.

The historical `Animation/assembly-mesh.json` is preserved as `assembly-mesh.json.gz`. `CompressedFiles.csv` records original/compressed sizes and hashes; decompressed SHA-256 was verified before removing the uncompressed copy. Restore the original filename before using an archived script that expects JSON. Review archived scripts for old absolute paths and existing model components before reuse.

Archive cleanup preserved unique CAD history, bench records, recovery assets and private configuration. Historical move/deletion details are recorded in the [repository validation history](REPOSITORY_VALIDATION.md). Chat-application attachment directories are app-managed and are not project dependencies.
