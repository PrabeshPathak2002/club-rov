# ROV service upgrade — PETG / H2D

## Current revision: rear horizontal pair corrected

The rear horizontal U01 pair is flipped back to its original orientation; the front horizontal and vertical pairs retain their reversed orientations. The relieved receiver pockets and fit coupon from the preceding revision still fit this configuration, so no additional reprint is needed for this correction. Use the current package rather than receiver STLs from before the pocket-relief revision. Mounting feet and fastener quantities are retained. Read the [orientation revision](../../Documentation/THRUSTER_ORIENTATION.md) and test the revised coupon before printing the full receivers.

Use S6_Hybrid_Service_Upgrade.f3d and the complete Service_Upgrade_STLs.zip together. The frame height, original enclosure, U01 thrusters, keyed mounting interfaces and both three-piece side frames are retained. The previous assembly is preserved under Archive/Design History 2026-09-29/S6Hybrid/service_upgrade/Before_service_upgrade.f3d at the project root.

## Changes to the printed parts

- Rear lower deck: dedicated centerline tether eye, two bridle attachment eyes, ballast mounting pad, two cable-tie pairs and two drain holes.
- Front lower deck: two bridle attachment eyes, ballast mounting pad, two cable-tie pairs and two drain holes.
- Four uprights: two outward-facing cable-tie guides each. Their openings are outside the original post section and away from the M3 insert seats.
- Both covers: a flush 70 x 52 mm outlined buoyancy mounting area, identification engraving and two additional 14 x 4 mm air-escape slots each. The existing cover slots accept retaining straps. The cover roof remains flat for roof-down printing.
- Physical labels identify FRONT, REAR, FOAM, BALLAST, TETHER and the FP/FS/RP/RS bridle positions. P is port, S is starboard, F is front, R is rear.

No rigid parts or screws were added. The package contains 27 assembly STLs and 10 fit coupons, 37 STL files total. Metal hardware remains 38 screws, 34 nuts, 56 washers and four heat-set inserts. Straps, ties, buoyancy material, ballast and any optional ballast fasteners are additional items and are not modeled or included in that count.

## Tether and cable routing

The rear tether eye is centered at X=-151, Y=0, Z=28 mm. Its 20 x 7 mm aperture is intended for a soft loop or strap attached to suitable tether-specific strain relief, not for clamping an unknown cable directly. The slot entrances have R0.8 edge rounds. Fit the actual strap/loop before use; the tether diameter remains unspecified.

Tether tension should transfer into the lower rear deck. Leave a slack service loop between strain relief and the enclosure connection. Never use the enclosure connector or a cable tie alone as the primary tether anchor. [Blue Robotics tether guidance](https://bluerobotics.com/store/cables-connectors/cables/fathom-rov-tether-rov-ready/) likewise calls for transferring tether pull away from the penetrator.

Four deck tie pairs have nominal 6 x 3 mm slots. Eight upright guides have 4 x 7 mm openings and R0.5 entrance rounds. Use ties no wider than 5 mm, subject to actual fit. Route branches below and inboard of the horizontal thrusters, then along the side uprights toward the rear enclosure connection. Keep service loops secured outside every propeller path. No cable diameter, minimum bend radius or flexible cable sweep has been verified; check these with actual cables and the thruster manufacturer's requirements. Remove sharp print residue without enlarging structural joints.

## Buoyancy and trim provisions

The two upper mounting areas are centered at X=+/-95, Y=0, Z=222 mm. Their outlines are 0.4 mm deep and their labels are 0.5 mm deep. Use the existing roof slots around Y=+/-32 and +/-48 mm for straps. Keep the added roof vents at X=+/-145, Y=+/-20 clear, and preserve screwdriver access at X=+/-84, Y=+/-75. Select depth-appropriate buoyancy material after the complete vehicle is weighed and its displacement assessed; printed PETG pads are not buoyancy foam.

The lower ballast pads are centered at X=+/-112, Y=0, Z=22 mm. Each has two 34 x 5.2 mm slots, centered at Y=+/-18 mm. These allow fore/aft adjustment using suitable M4 hardware or straps up to 25 mm wide, after checking actual fit and retention. Keep weights low and within the enclosure/attachment clearances. Added ballast and its fasteners are not yet sized. Adjust and test trim in water; [Blue Robotics' operating guide](https://bluerobotics.com/learn/bluerov2-operation/) describes checking buoyancy and adjusting ballast.

## Handle and bridle load-path review

The present handle transfers lifting force through its two M4 bolts into the two top beams. Upward beam loads then reach the four M3 insert joints, the four uprights, their foot bolts, and the lower decks. The locating keys resist lateral motion; they do not independently retain the beam against upward separation. Insert pullout and PETG creep are therefore relevant to lifting. This review does not establish a working load limit.

Four lower-frame eyes at X=+/-120, Y=+/-55, Z=28 mm provide attachment provisions for a separately designed bridle or recovery system. Each has a rounded 20 x 7 mm opening through a 12 mm thick eye, with R0.8 entrance rounds. A properly arranged system could take loads directly into the lower decks, but a complete rigging route, strap selection and rating have NOT been established. These are not certified lifting eyes. A straight converging bridle can contact the enclosure or covers; do not assume that fitting four straps creates an acceptable lifting arrangement. Select and check the full rigging geometry with the actual vehicle and, if needed, a spreader arrangement before using the points.

Once the finished mass is known, test representative PETG/insert joints and the complete handle and proposed recovery arrangement separately. Use a restrained bench setup, measure load and displacement, and inspect for insert movement, cracking, layer separation, screw loosening and time-dependent deformation. The small fit coupons do not substitute for complete-frame load tests. No physical testing or new FEA was performed in this revision, and no load rating is assigned.

## Future thruster adapters

The Thruster Interface References folder contains six STEP base references and a dimension/placement manifest. Existing mounts already use two keyed feet and one M4 retainer per station. Retain these interfaces and design a new upper support around the actual future thruster CAD. The horizontal patterns are handed; the vertical mounts are a separate interface family. These reference solids are not complete mounts and should not be printed as operational adapters.

Keep the 14 mm installation slide, bearing shoulders, locking bore and tool access. Check actual duct and propeller clearances, inlet/outlet flow space, cables, cover openings, thrust loads and print orientation. Current frame height is preserved, but no unspecified larger thruster is guaranteed to fit.

## Printing and assembly

PETG with 100% infill, as requested. Use the per-part orientation and notes in service_print_exports.json. All parts are checked against the conservative H2D 300 x 320 x 325 mm envelope, with 3 mm brim allowance. The revised lower decks are supplied skids/underside down, with their new eyes upright; removable support is needed beneath unsupported deck areas and channels, and may be needed at the eye apertures. This replaces the old top-face-down orientation. Inspect the slicer preview and support raised guide features where required. Flush cover markings preserve the original broad roof-down printing surface. Infill percentage does not remove layer-direction weakness.

Print TEST FIRST strap eye and TEST FIRST cable guide with the actual intended settings, and check strap/tie passage, surface finish and retention. Also retain the existing rail, T-key, enclosure-liner and M3-joint coupon checks. The eye coupon is cropped from the frame; it is for fit only and does not reproduce full-frame loading.

M3 inserts retain the previous design basis: 4.6 mm maximum OD x 5.7 mm length, in nominal 4.0 mm pilots. Confirm purchased insert dimensions and fit. Install inserts before fitting the keyed beams. The four M3x30 screws also pass through the covers; the 4 mm cover or coupon spacer is required to reproduce their grip length. All existing foot, collar, cross-clamp, handle and deck bolts remain required.

For servicing, first release ties and connectors as needed and free the strain-relieved service loop. The rigid-model end-cap corridors are not a substitute for disconnecting wiring. Remove the covers for broad access. Do not force an enclosure tray or attached electronics through the cap corridor without checking its complete envelope.

## Geometry checks and remaining limits

The assembly is checked for unintended interference, single-solid integrity, timeline health and hardware count. service_verification.json records nominal 126 mm diameter, 120 mm axial end-cap corridors; 8 mm driver approach corridors above the four M3 cover screws; and sampled removal motions of all six mounted U01 modules against the revised frame parts. Sampling is a clearance screen, not a continuous-motion proof.

The intentional overlaps between heat-set insert envelopes and their pilot holes are retained. Drain and vent openings are kept away from the insert bosses, keyed mounting feet and primary joint seats. Verify actual drainage and air release during a water test, particularly once foam, straps and cables are fitted.

All exported STLs are checked for watertightness, winding, millimetre scale and agreement with native solid volume. See final_verification.json, service_stl_verification.json and service_verification.json for the measured results. Strength, wet-service durability, PETG creep, tether retention, complete rigging and future thruster compatibility remain unqualified.
