# ARK below / ESC above — printable stack adapter

The adapter stacks the ESC above the ARK to fit both boards in the enclosure. The selected ESC has **20 x 20 mm mounting-hole spacing**, while the ARK uses 30.5 x 30.5 mm, so the adapter bridges the two patterns. The adapter uses the measured ESC spacing; a different GT 55A drawing shows a 30.5 mm pattern.

## Print files

- `ARK_ESC20_adapter_print_flat.stl`: print one, flat face on the bed, bosses up.
- `M3_spacer_7mm_print_4.stl`: print four, upright, hole vertical.
- STEP files provide editable solid geometry for the plate and assembly.

STLs use millimetres. Verified exported sizes are 40 x 40 x 6 mm for the adapter
and 6 mm diameter x 7 mm height for a spacer. Both meshes passed closed-manifold
edge checks. The adapter is one solid, with a 3 mm web and four 3 mm raised ESC
mounting bosses. Rounded diagonal arms leave the sides open for connectors.
The centre opening is 11 mm diameter. All fastener holes are 3.4 mm M3 clearance.

The starting H2D print setup is PETG at 0.20 mm layers, five walls and 100% infill. Neither part needs supports in the
stated orientation. Use a small brim on the spacers if needed for adhesion.
Check one printed hole/spacer fit before assembling the full stack.

## Assembly order

Existing tray / ARK lower isolation hardware -> ARK PCB -> existing upper
grommet hardware -> four 7 mm spacers at the 30.5 mm pattern -> adapter ->
ESC isolation hardware on the raised 20 mm bosses -> ESC PCB -> retainers.

The outer four holes retain the adapter above the ARK. The inner four holes
mount the ESC, with M3 hardware passing through its existing suitable grommets.
Keep the ARK's soft mounts; the printed spacers do not replace its vibration
isolation. Use nuts or suitable standoffs for retention, not threads cut into
the 3.4 mm clearance holes. Screw lengths depend on actual grommet, washer,
tray and nut thicknesses and have not been selected from the CAD alone.
Avoid clamping directly on populated PCB surfaces or crushing the grommets.

## Fit verification and limits

The stack in the source Fusion assembly is aligned to the ARK model's hole centres at
X=-8.484 mm, Y=0. Plate underside is at assembly Z=130 mm, upper web at 133 mm,
and raised ESC seating surfaces at 136 mm. The tallest modeled ARK component
is at Z=125.189 mm: nominal plate-to-component clearance is 4.81 mm.

The CAD check found no solid intersections between the adapter/four spacers and the
visible original enclosure/tray/electronics geometry (five bounding-box overlap
candidates checked using solid intersections). This does not establish cable,
plug, nut or solder-joint clearance. The actual ESC is not modeled: the old
36 mm envelope was provisional. Dry-fit the real board, its underside parts,
soft mounts, battery solder joints and USB/JST cables before final assembly.
Thermal and structural load tests remain pending.

## CAD reference

The adapter was created in the `Future ROV` Fusion assembly under `09 ARK ESC printable stack - FC below ESC20 above - M3`.
The source assembly retained an earlier exploded `ESC upper stack - middle adapter` as a hidden reference. The export operation left the original tray and ARK geometry/position unchanged. These assembly-history notes do not imply that the complete Fusion document is included here.
The STL and STEP exports in this folder are the files to use for this revision. Adapter fit has been checked; complete wiring and connector clearance remain to be verified.

`stack_assembly_preview.png` shows the ARK/tray and adapter, with the enclosure
temporarily hidden for viewing. No actual ESC PCB is represented in the preview.
