# Final ROV design

Current revision: M4 side-frame joints, 30 September 2026. Start with the assembly and printing guide below.

Current orientation revision: rear horizontal pair flipped back; front horizontal and vertical pairs retain their reversed orientations. The current relieved receiver pockets and print files are unchanged by this correction. See [revision details and fit-test requirements](#orientation-and-checks). Earlier validation reports are historical where superseded by the rear-pair correction reports.

- [Complete Fusion assembly](CAD/S6_Hybrid_Service_Upgrade.f3d)
- [Print-oriented Fusion parts](CAD/Service_Upgrade_Print_Parts.f3d)
- [Frame STEP export](CAD/Service_Upgrade_Frame.step) — frame component only, not the complete enclosure/thruster assembly
- [Print package](Prints/Service_Upgrade_STLs.zip) — 27 assembly parts, 10 fit coupons, guides, BOM, checks and adapter references
- [Individual STL files](Prints/Service_Upgrade_STLs/)
- [BOM spreadsheet CSV](BOM/Current_ROV_BOM.csv) and [BOM notes](BOM/Current_ROV_BOM.md)
- [Printing and assembly guide](Guides/PRINT_GUIDE.md)
- [Validation reports](Validation/) and [design previews](Previews/)
- [Future thruster adapter references](Thruster%20Interface%20References/README.md)

Current hardware: 38 M4 custom frame/mount screws, 34 nuts, 56 washers and four M4 inserts. The reused enclosure/electronics CAD also contains 30 screw solids. The BOM explains exclusions and unspecified accessories.

## M4 frame joints

The four side-frame screws are now M4x30, with four M4 heat-set inserts (6 mm maximum OD x 8.1 mm length) and 9x4.4x0.8 mm washers. The beam holes are 4.5 mm; insert pilots are 5.6 mm diameter and 10.2 mm deep. Four uprights and two beams must be printed from the new M4 files. Test all three M4 joint coupons with the actual inserts before full printing. All 38 custom screws now use M4 threads; the imported enclosure hardware remains separately specified.

The conversion passed 71 candidate intersection checks with only the four intentional insert/pilot overlaps, plus 402 sampled service checks. All 37 STLs passed watertightness, winding, scale and solid-volume checks. Thruster transforms were retained and Fusion reported no timeline issues. These checks do not establish joint strength or a lifting rating.

## Orientation and checks

The rear port and rear starboard horizontal thrusters are flipped back to their original orientation. The front horizontal and vertical pairs retain their reversed orientations, giving four vectored horizontal and two vertical units. The current relieved receiver pockets still fit; this correction does not change printed parts or fastener quantities.

The rear-pair correction passed six candidate intersection checks and all 402 sampled service-clearance checks, with no Fusion timeline warnings or errors. The other four thruster transforms were verified unchanged. These are nominal CAD checks; physical rail fit, cable routing, motor mapping, and actual thrust directions still need testing.

This is the current design release, not a physically qualified vehicle. Lifting/attachment loads, final electronics, tether, buoyancy and ballast still require the checks described in the guide. Ten test coupons are not installed parts. The six adapter references are not complete working mounts.

Earlier revisions, experiments, scripts, raw tool logs and backups are preserved at ../Archive/Design History 2026-09-29/S6Hybrid/. No original design-history files were deleted. The archive inventory and restore instructions are in that archive's parent folder. Original source models, code and other project documents remain in their original project folders.
