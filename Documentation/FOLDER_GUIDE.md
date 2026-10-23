# Folder guide

The consolidated working checkout is `ClubROV` inside the project workspace. Open it for current documentation and source changes. The older OneDrive checkout is preserved and should not be edited in parallel without deliberately synchronizing changes.

- `Code/ROV ESP32-P4 code/`: current companion firmware, editable source, tests, dependency lock and local ignored credentials/build outputs.
- `Documentation/`: current setup, status, BOM, learning path and test-report template.
- `Hardware/`: current selected original-part exports and fit-study notes.
- `Models/`: historical complete assembly viewer.
- `Final Design/`: September frame/BOM baseline with explicit supersession notices.

The parent workspace's `Archive/Working History 2026-10-23/` preserves the original Animation, CameraBringup, MountRevision, TrayRevision and EndcapRevision folders. Its CSV records original/destination paths, sizes and SHA-256 hashes. Historical scripts may contain the old absolute working paths; preserve them as records rather than rerunning them without review. Credentials and logs in that archive are private and excluded from publication.

The OneDrive project's existing `Archive/Design History 2026-09-29/` and commercial reference library remain in place. Nothing in either archive was intentionally deleted by this cleanup. Fusion documents currently open in the application were not saved, closed or replaced by the filesystem cleanup.
