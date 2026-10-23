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
