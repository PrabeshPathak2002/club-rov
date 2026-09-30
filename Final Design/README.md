# Final ROV design

Current service-upgrade revision, organized 29 September 2026. Start with the assembly and printing guide below.

Current orientation revision: all six thrusters reversed, with matching relief in the six fixed receivers. See [revision details and fit-test requirements](../Documentation/THRUSTER_ORIENTATION.md). Earlier validation reports are historical where superseded by the thruster-reversal reports.

- [Complete Fusion assembly](CAD/S6_Hybrid_Service_Upgrade.f3d)
- [Print-oriented Fusion parts](CAD/Service_Upgrade_Print_Parts.f3d)
- [Frame STEP export](CAD/Service_Upgrade_Frame.step) — frame component only, not the complete enclosure/thruster assembly
- [Print package](Prints/Service_Upgrade_STLs.zip) — 27 assembly parts, 10 fit coupons, guides, BOM, checks and adapter references
- [Individual STL files](Prints/Service_Upgrade_STLs/)
- [BOM spreadsheet CSV](BOM/Current_ROV_BOM.csv) and [BOM notes](BOM/Current_ROV_BOM.md)
- [Printing and assembly guide](Guides/PRINT_GUIDE.md)
- [Validation reports](Validation/) and [design previews](Previews/)
- [Future thruster adapter references](Thruster%20Interface%20References/README.md)

Current hardware: 38 custom frame/mount screws, 34 nuts, 56 washers and four M3 inserts. The reused enclosure/electronics CAD also contains 30 screw solids. The BOM explains exclusions and unspecified accessories.

This is the current design release, not a physically qualified vehicle. Lifting/attachment loads, final electronics, tether, buoyancy and ballast still require the checks described in the guide. Ten test coupons are not installed parts. The six adapter references are not complete working mounts.

Earlier revisions, experiments, scripts, raw tool logs and backups are preserved at ../Archive/Design History 2026-09-29/S6Hybrid/. No original design-history files were deleted. The archive inventory and restore instructions are in that archive's parent folder. Original source models, code and other project documents remain in their original project folders.
