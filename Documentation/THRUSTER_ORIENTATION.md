# Thruster reversal revision

All six APISQUEEN U01 assemblies were reversed by 180 degrees in Fusion at the project owner's request. Each rotates about its rail-normal axis through the receiver cross-section center so the attached rail remains on the mounting side. The source U01 reference document is unchanged.

Affected stations: H Front Port, H Front Starboard, H Rear Port, H Rear Starboard, V Port, and V Starboard.

## Mechanical changes

The asymmetric rails interfered with the original fixed receivers when reversed. Each of the six fixed receiver pockets was relieved around the reversed rail using translated cutter samples with a 0.25 mm allowance. Approximately 220 mm³ was removed per receiver; each receiver remains a single solid. The feet, frame attachment positions, and fastener quantities are retained.

Use the revised six `fixed_receiver_and_support.stl` files and revised rail-fit coupon from the current local print package. Do not combine the reversed assembly with the earlier receiver prints. Removable rail jaws and the rest of the printed frame retain their designs.

## Checks and limits

The pre-application check found no volumetric clashes above 0.1 mm³ in 99 candidate intersection pairs involving the changed geometry and visible surrounding parts. This is a nominal CAD check, not a strength, flow, cable-clearance, or physical fit qualification. The cutter samples are not a uniform surface offset; print and fit-test the revised coupon before the full receivers.

The 402 sampled service checks also passed, including mount removal motions, end-cap service corridors, and screwdriver access to the top-frame screws.

All 37 regenerated STL files passed watertightness, winding, millimetre-scale, and volume checks. The package contains 27 installed print parts and 10 fit coupons. Maximum mesh-to-solid volume difference was 0.0111%.

The rotation reverses the modeled axial direction. Actual positive thrust, motor channel mapping, ESC direction, and ArduSub motor settings must be verified on the assembled vehicle. No firmware settings were changed by this mechanical revision.

The detailed GitHub STL and README image are regenerated from the revised assembly. Local reports and the pre-change Fusion backup are under `Documentation/thruster_flip/`; the earlier complete design package is preserved under `Archive/Before thruster reversal 2026-09-29/`.
