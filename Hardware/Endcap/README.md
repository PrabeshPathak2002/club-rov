# Quarter-inch acrylic endcap — LP20 fit study

## Circular five-LP12 revision — 18 November 2026

[Circular-pattern DXF](Endcap_6p35_5LP12_circular_LP20_fit_layout.dxf) and [matching SVG](Endcap_6p35_5LP12_circular_LP20_fit_layout.svg) place five LP12 D openings on a 76 mm pitch circle centered on the retained Ethernet opening. DXF center angles are 54, 126, 198, 270 and 342 degrees. Four 3-contact LP12 pairs serve the individual horizontal thrusters, and one 6-contact pair carries the two vertical thrusters on separate contacts. All D flats retain their original orientation. Perimeter, six fixing holes, LP20 double-flat opening and two M10 openings are unchanged. There are 15 closed cutting contours and no kerf compensation.

Minimum LP12 center spacing is 44.67 mm. The minimum nominal cut-edge gap involving the LP12 openings is 8.35 mm. The closest LP12-to-M10 center distance is approximately 19.74 mm, so this location limits hardware clearance. A 19.3 mm circular LP12 body envelope and a 20 mm M10 hardware envelope would leave only about 0.09 mm there. The plate has been laser-cut, as reported by the builder. Installed nut/seal clearance, assembly access, vacuum testing and pressure qualification remain pending. The close LP12/M10 spacing requires physical inspection. Fusion and STEP exports still represent the earlier revision.

## Earlier three-LP12 revision

This endcap layout targets 6.35 mm acrylic with three motor connectors, Ethernet, a depth-sensor port and a vacuum/pressure-relief port. Physical fitting checks and pressure qualification remain pending.

Earlier exports: [SVG](Endcap_6p35_LP20_fit_layout.svg), [DXF](Endcap_6p35_LP20_fit_layout.dxf), [STEP](Endcap_6p35_LP20.step). No kerf compensation is included. Cut the [LP12 coupon](LP12_6p35mm_fit_coupon.svg) and [LP20 coupon](LP20_6p35mm_fit_coupon.svg) in actual stock first.

![Fusion fit study](LP20-fusion-preview.png)

- Thickness: 6.35 mm; original perimeter and six 3.2 mm fixing holes retained.
- Three LP12 openings: single-flat D, underlying diameter 12.10 mm and overall height 11.60 mm.
- Two M10 clearance openings: 10.20 mm each, for Bar02 and PRV/vacuum.
- One LP20 opening: double-flat, underlying diameter 22.60 mm and 20.50 mm across flats.
- Total cutting contours: one perimeter, six fixing holes, six main openings = 13 closed contours.

LP12 coupon left/middle/right: diameter/height 12.00/11.50, 12.10/11.60, 12.20/11.70 mm. LP20 coupon left/middle/right: diameter/across-flats 22.50/20.40, 22.60/20.50, 22.70/20.60 mm. Each coupon is 100 × 35 mm.

The LP20 fitting was selected after considering M20 and LP16 options. This layout replaces the earlier drawings that omitted the Ethernet opening. The selected LP20 mounting thread length and allowable panel thickness are not specified in the available drawing: physically check seal seating, nut engagement and tool clearance through the 6.35 mm plate. Do not enlarge holes or counterbore the pressure plate merely to force a fitting to fit.

One-solid, thickness, contour and analytic-volume checks passed. Those checks do not establish acrylic strength, watertightness or depth capacity. The referenced LP20 specification states IP68 at 2 m for 48 hours; it does not establish 20–50 m use. Reducing the original 12.7 mm blank to 6.35 mm and adding ports requires separate structural and pressure qualification. The assembly's flange/seal interface remains to be checked.

Sources: LP12 and LP20 mechanical drawings for the selected fittings; [Bar sensor guide](https://bluerobotics.com/learn/bar-sensors-guide/); [Blue Robotics PRV](https://bluerobotics.com/store/watertight-enclosures/enclosure-tools-supplies/prv-m10-asm/). Original screenshots and datasheets are private design references, not files included in this checkout. Verify the ordered connector revision against its manufacturer drawing before using these fit-study exports.
