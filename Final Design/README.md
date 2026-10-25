> This is the September 2026 baseline. Later mounts, electronics and endcap revisions supersede parts of this document. Use the [current revision index](../Hardware/README.md) before building or ordering. Historical totals are not a current purchasing list.

# September 2026 frame design baseline

The side-frame joints were standardized to M4 on 30 September 2026. The notes below describe that revision; use the current hardware revision index above before fabrication.

September orientation revision: rear horizontal pair flipped back; front horizontal and vertical pairs retain their reversed orientations. The September relieved receiver pockets and print files are unchanged by this correction. See [revision details and fit-test requirements](#orientation-and-checks). Earlier validation reports are historical where superseded by the rear-pair correction reports.

- Complete Fusion assembly (historical local-only asset: `CAD/S6_Hybrid_Service_Upgrade.f3d`)
- Print-oriented Fusion parts (historical local-only asset: `CAD/Service_Upgrade_Print_Parts.f3d`)
- Frame STEP export (historical local-only asset: `CAD/Service_Upgrade_Frame.step`) — frame component only, not the complete enclosure/thruster assembly
- Print package (historical local-only asset: `Prints/Service_Upgrade_STLs.zip`) — 27 assembly parts, 10 fit coupons, guides, BOM, checks and adapter references
- Individual STL files (historical local-only asset: `Prints/Service_Upgrade_STLs/`)
- [September CAD inventory](BOM/September_2026_CAD_Inventory.md) and [original detailed CSV](BOM/September_2026_CAD_Inventory.csv) — historical reference; use the [current ROV BOM](../Documentation/BOM.md) for build selections
- [Printing and assembly guide](Guides/PRINT_GUIDE.md)
- Validation reports (historical local-only asset: `Validation/`) and design previews (historical local-only asset: `Previews/`)
- Future thruster adapter references (historical local-only asset: `Thruster Interface References/README.md`)

September baseline hardware: 38 M4 custom frame/mount screws, 34 nuts, 56 washers and four M4 inserts. The reused enclosure/electronics CAD also contains 30 screw solids. The BOM explains exclusions and unspecified accessories.

## M4 frame joints

In this revision, the four side-frame screws use M4x30, with four M4 heat-set inserts (6 mm maximum OD x 8.1 mm length) and 9x4.4x0.8 mm washers. The beam holes are 4.5 mm; insert pilots are 5.6 mm diameter and 10.2 mm deep. The matching four uprights and two beams use the M4 revision files. Test all three M4 joint coupons with the actual inserts before full printing. All 38 custom screws in this baseline use M4 threads; the imported enclosure hardware remains separately specified.

The conversion passed 71 candidate intersection checks with only the four intentional insert/pilot overlaps, plus 402 sampled service checks. All 37 STLs passed watertightness, winding, scale and solid-volume checks. Thruster transforms were retained and Fusion reported no timeline issues. These checks do not establish joint strength or a lifting rating.

## Orientation and checks

The rear port and rear starboard horizontal thrusters are flipped back to their original orientation. The front horizontal and vertical pairs retain their reversed orientations, giving four vectored horizontal and two vertical units. The September relieved receiver pockets still fit; this correction does not change printed parts or fastener quantities.

The rear-pair correction passed six candidate intersection checks and all 402 sampled service-clearance checks, with no Fusion timeline warnings or errors. The other four thruster transforms were verified unchanged. These are nominal CAD checks; physical rail fit, cable routing, motor mapping, and actual thrust directions still need testing.

This is a historical design baseline, not the current complete vehicle release or a physical qualification record. Lifting/attachment loads, final electronics, tether, buoyancy and ballast still require the checks described in the guide. Ten test coupons are not installed parts. The six adapter references are not complete working mounts.

The CAD, STL packages, validation reports and interface-reference files labeled local-only above are retained in private maintainer archives and are not supplied by cloning this repository. Their names identify the historical source set. See the [folder guide](../Documentation/FOLDER_GUIDE.md#private-maintainer-archives) for archive context and [Hardware](../Hardware/README.md) for public revision files.
