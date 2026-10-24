# Repository validation — 23 October 2026

These are the build and repository checks for the source package I published:

- Fresh ESP-IDF 5.5.2 configuration and full build passed from the consolidated directory, with the existing dependency lockfile unchanged.
- P4 target: pre-v3 bench-board revision setting, 32 MB flash, IMX219 format index 2, 100 Hz scheduler and custom OTA partition table.
- Application size: 1,145,920 bytes; 73% of the smallest 4 MiB application partition free. This image was not flashed during repository cleanup and is excluded from Git because it embeds local credentials.
- Host framing tests: 110,979 exact frame deliveries passed, covering fragmentation, signed frames, boundaries and malformed datagrams.
- Host heartbeat tests: 168 cases passed for CRC, arm state, MAVLink v1/v2, malformed input and controller identity.
- Reviewed publication set: no deployed Wi-Fi password, populated credential header, binary, build directory, serial log or complete local sdkconfig tracked. First-party Markdown file links resolve.
- Text/source whitespace check passed. Native STEP exporter comment whitespace is preserved in the CAD files.
- Original workspace history: 371 files, 840,717,358 bytes moved into the private dated archive and verified by size and SHA-256. I kept the earlier OneDrive checkout and its archive.

The GitHub workflow runs the portable host tests on pushes and pull requests. The firmware build above ran locally; I have not configured hosted ESP-IDF builds. None of these checks establishes motor behavior, enclosure pressure capacity or field reliability. See [physical integration status](STATUS.md).

## Workspace cleanup — 24 October 2026

I archived 944 files from obsolete practice packages, redundant extracted players, a player log and the Unity recovery scene. Each move was verified by size and SHA-256; the private archive contains the inventory and original/destination mapping. Nothing was deleted. Source, CAD files, credentials, active firmware builds, Unity caches, SITL installation and app-managed attachments were preserved.

The practice packager now streams directly to a ZIP and publishes it only after the archive closes successfully. A real packaging run passed ZIP integrity checks; all 185 included player files matched the active Windows build by SHA-256. The quickstart matched its source, the launcher had no byte-order mark, debug/backup folders were excluded and no extracted staging directory was created. This packages an existing compiled player; it does not rebuild or validate new simulator source changes.

The active Builds folder now contains Windows and one shareable ZIP. Recovery paths are ignored. The folder guide and simulation overview were refreshed, and source whitespace checks passed. No new firmware flash, Unity player build or hardware test was performed for this cleanup.
