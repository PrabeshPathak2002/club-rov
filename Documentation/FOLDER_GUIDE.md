# Folder guide

I use `ClubROV` as the current working checkout for source and documentation. I kept the older OneDrive checkout as a backup; I avoid editing both copies in parallel so they do not drift apart.

- `Code/ROV ESP32-P4 code/`: current companion firmware, editable source, tests, dependency lock and local ignored credentials/build outputs.
- `Documentation/`: current setup, status, BOM, learning path and test-report template.
- `Hardware/`: current selected original-part exports and fit-study notes.
- `Models/`: historical complete assembly viewer.
- `Final Design/`: September frame/BOM baseline with explicit supersession notices.

I moved the original Animation, CameraBringup, MountRevision, TrayRevision and EndcapRevision folders to the parent workspace's `Archive/Working History 2026-10-23/`. The archive CSV records original/destination paths, sizes and SHA-256 hashes. Historical scripts may contain the old absolute working paths; preserve them as records rather than rerunning them without review. Credentials and logs in that archive are private and excluded from publication.

The OneDrive project's existing `Archive/Design History 2026-09-29/` and commercial reference library remain in place. I preserved the original files and verified the moved working-history files by size and SHA-256. The folder cleanup did not change the Fusion documents.
