# Club Demo ROV wiring layout study

This packaging study was created in `Club_Demo_ROV` on 21 October 2026 to check battery, connector and ESC fit in the electronics area. Existing hardware was retained, and the old Turnigy battery envelope was replaced with the Zeee pack. The holders and busbars still need physical dimensions and fit checks before fabrication.

## Contents

- Zeee 4S 3300 mAh battery: 132 x 43 x 29 mm nominal. Separate hidden 134 x 45 x 31 mm envelope represents +2 mm on each overall dimension. Battery leads, balance plug, padding, straps, and swelling are additional allowances, not included in that tolerance.
- Six provisional MR30 mated-pair envelopes, 32 x 13 x 7 mm, in staggered side rows. These are intentionally simple clearance solids, not detailed connector CAD. Measure actual mated connectors including insulation before releasing holders.
- Four provisional XT60 mated-pair envelopes, 40 x 18 x 10 mm, above the rear electronics area. A removable strap bridge holds these above the FC/ESC stack; exact terminal shapes and release travel remain to be checked.
- Two Blue Robotics Basic ESC body envelopes, 51.29 x 17.10 x 7.28 mm, beside the battery. Dimensions were measured from the manufacturer's BESC30-R3 STL. Cable exits, heat shrink and strain relief may require more space. Open cradles leave the heat spreader accessible.
- A 36 x 40 x 12 mm reserved envelope for the upper four-in-one ESC; its exact model and stack adapter are not changed here.
- Two provisional copper bars, 34 x 10 x 6.35 mm with four 4.4 mm clearance holes on 8 mm centers. Approximately quarter-inch-thick copper stock is available; its width and actual terminal dimensions require measurement. These are packaging placeholders, not a drilling template or a verified electrical rating. Ring-terminal width, bolt-head/nut/washer spacing and insulation must be checked before fabrication; choose wider spacing if terminals require it.
- Separate insulating junction cradle and removable strap-retained cover; terminal hardware and cable exit openings are not detailed yet.
- Provisional low-profile right-angle Ethernet plug reservation and schematic Ethernet, Bar02 and propulsion cable paths. These paths illustrate routing intent only: their mutual clearances, total bundle size, bend radii, connector unplugging space, strain relief and endcap penetrator assignment have not been validated. They are not manufactured wire lengths.

## Clearance findings

The script checks positive-volume solid intersections between the new physical packaging bodies and the existing visible assembly, and among new physical bodies. Bodies are transformed through the full native occurrence hierarchy, including nested imported electronics.

At the original FC position, the remaining physical collision was the existing `91290A124` FC mounting screw protruding into the Zeee battery. A suitable shorter screw would be required after measuring the actual stack and thread engagement. Do not compress the battery around the screw. The screw remains in the model to preserve visibility of the interference until a replacement is fitted.

The ARK and its mounting screw were restored to their original lower position after a displacement of approximately 17.8 mm was identified. Lowering them by 17.77 mm kept their horizontal position and orientation unchanged. The repeated clearance check found no overlaps among the new physical bodies and no existing-hardware overlaps except the long FC screw entering the battery. Exact connectors and cable routing still require verification. The archived clearance report uses that restored position.

The hidden tolerance envelope is not included in the physical-body collision pass. Cable paths and wire-exit allowances are also excluded. The holder interfaces are trimmed against existing tray/rail geometry; they still require a physical fit and retention review. No printable STL files are released from this study.

## Bar02 connection

The ARK FPV six-pin GPS port includes 5 V on pin 1, SCL on pin 4, SDA on pin 5 and ground on pin 6. Pins 2 and 3 are UART and are unused for Bar02. Verify the connector's numbered orientation before repinning. The Bar02 harness reservation is schematic and does not identify a physical endcap hole.

## References

- ARK pinout: https://docs.arkelectron.com/products/flight-controller/ark-fpv/hardware
- Blue Robotics Basic ESC: https://bluerobotics.com/store/thrusters/speed-controllers/besc30-r3/
- Official Basic ESC CAD: https://cad.bluerobotics.com/BESC30-R3.zip
- Bar sensor connection guide: https://bluerobotics.com/learn/bar-sensors-guide/
- AMASS MR30 catalog drawings: https://evelta.com/content/datasheets/515-amass-connectors.pdf
- AMASS XT60H drawing: https://www.shoptronica.com/ficheros/XT60H.pdf

## Files

This directory contains the notes and preview. The layout script, clearance report and concept STEP are retained in the private working-history archive. Historical scripts contain old absolute paths and must be reviewed before reuse; do not rerun a commit against an existing WM component.
