# Repository validation — 23 October 2026

The published source package was checked as follows:

- Fresh ESP-IDF 5.5.2 configuration and full build passed from the consolidated directory, with the existing dependency lockfile unchanged.
- P4 target: pre-v3 bench-board revision setting, 32 MB flash, IMX219 format index 2, 100 Hz scheduler and custom OTA partition table.
- Application size: 1,145,920 bytes; 73% of the smallest 4 MiB application partition free. This image was not flashed during repository cleanup and is excluded from Git because it embeds local credentials.
- Host framing tests: 110,979 exact frame deliveries passed, covering fragmentation, signed frames, boundaries and malformed datagrams.
- Host heartbeat tests: 168 cases passed for CRC, arm state, MAVLink v1/v2, malformed input and controller identity.
- Reviewed publication set: no deployed Wi-Fi password, populated credential header, binary, build directory, serial log or complete local sdkconfig tracked. First-party Markdown file links resolve.
- Text/source whitespace check passed. Native STEP exporter comment whitespace is preserved in the CAD files.
- Original workspace history: 371 files, 840,717,358 bytes moved into the private dated archive and verified by size and SHA-256. The earlier OneDrive checkout and its archive were retained.

The GitHub workflow runs the portable host tests on pushes and pull requests. The firmware build above ran locally; hosted ESP-IDF builds are not configured. None of these checks establishes motor behavior, enclosure pressure capacity or field reliability. See [physical integration status](STATUS.md).

## Workspace cleanup — 24 October 2026

The cleanup archived 944 files from obsolete practice packages, redundant extracted players, a player log and the Unity recovery scene. Each move was verified by size and SHA-256; the private archive contains the inventory and original/destination mapping. Nothing was deleted. Source, CAD files, credentials, active firmware builds, Unity caches, SITL installation and app-managed attachments were preserved.

The practice packager now streams directly to a ZIP and publishes it only after the archive closes successfully. A real packaging run passed ZIP integrity checks; all 185 included player files matched the active Windows build by SHA-256. The quickstart matched its source, the launcher had no byte-order mark, debug/backup folders were excluded and no extracted staging directory was created. This packages an existing compiled player; it does not rebuild or validate new simulator source changes.

The active Builds folder now contains Windows and one shareable ZIP. Recovery paths are ignored. The folder guide and simulation overview were refreshed, and source whitespace checks passed. No new firmware flash, Unity player build or hardware test was performed for this cleanup.

## Documentation and archive retention — 24 October 2026

Project reference guides were rewritten in a neutral voice while retaining dated test evidence, pending work and existing technical instructions. Validation found no new broken local Markdown links or changes to fenced command/code blocks. `git diff --check` passed. No firmware or simulator source changed during this pass, so application builds and runtime tests were not repeated.

The private archive retention pass verified obsolete package files against their size/SHA-256 inventory before deleting 940 generated files across ten package roots. The archived assembly mesh was gzip-compressed; its decompressed SHA-256 matched the original before removal of the uncompressed copy. Deletion and compression manifests remain beside the original inventories. The active Windows player, latest practice ZIP, installed QGroundControl, Unity recovery scene and archived startup log were confirmed present. Approximately 1.67 GiB was recovered. Original move inventories describe the earlier cleanup; consult the subsequent manifests for current restoration availability.
