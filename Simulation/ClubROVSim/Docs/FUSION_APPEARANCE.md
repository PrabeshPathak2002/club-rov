# Simulator appearance

Color reference: `ClubROV/Models/preview.png`, the Fusion illustration of the same September assembly used by the simulator STL. Yellow deck/handle, graphite frame/base, green thruster housings, red rotors and silver enclosure replace the earlier arbitrary teal scheme. The enclosure is opaque silver in this simplified display; acrylic transparency and internal electronics are not reproduced.

`RovAppearance` clones the visual mesh and materials at runtime and releases them on destruction. Original mesh assets, colliders and physics remain unchanged. `RovDynamics.Awake` installs the appearance for new sessions.

`Assets/ROV/Resources/RovFusionColors.bytes` contains one palette index per original STL triangle, in its original order. Palette: 0 yellow, 1 graphite, 2 green, 3 red, 4 silver. Source: `Models/club-rov-preview.stl`, 82,560 triangles, SHA256 `973fe9434536795bb15b75f4563b56f6d0d918df5e6b3504b7bf01b268687563`.

The map was assigned using welded connected components (vertex coordinates rounded to 0.001 mm): six 899-triangle housing components are green, six 882-triangle rotor/motor components red, enclosure components silver, the handle yellow and frame/mount components graphite. The welded main frame uses yellow above CAD Z=200 mm for the upper deck. This is a visual approximation of the reference, not a per-body Fusion material export. Regenerate the map if the source mesh changes; do not reuse it for another mesh with the same triangle count.

Validation: five populated material regions, original triangle count preserved, Unity compilation without errors or warnings, and live Game-view inspection against the reference.

## Rotor animation

RovAppearance partitions the existing red rotor/motor geometry into six 882-triangle runtime meshes. Each pivot is projected onto the CAD shaft line from RovThrusterLayout. The former asymmetric bounds-centre pivots were 3.19–5.05 mm off-axis. Corrected swept radii are 30.79–30.86 mm, inside the nominal 31 mm housing bore. Original surface geometry and zero-angle placement are preserved; green housings remain fixed. No colliders or forces are added.

Signed, health-adjusted actual outputs drive the angles on PhysicsStepped; transforms update once per rendered frame. Reverse reverses spin, zero output stops it, reset clears angles, and enable/disable balances subscriptions. Local and externally stepped physics use the same event.

Full output uses **3,000 visual RPM**, linear with output fraction. This is a simulation estimate, not verified U01 loaded RPM. The [manufacturer U01 page](https://www.underwaterthruster.com/products/apisqueen-12v-16v-2kg-thrust-u01-tow-set-brushless-underwater-thruster-propeller-with-bi-directional-control-esc-for-rov-boat) and its [performance chart](https://cdn.shopify.com/s/files/1/0621/5493/2452/files/01b686ed25c44de3319994114885462.png?v=1715226865) were checked on 1 November 2026: the chart gives voltage, throttle, current and thrust, but no RPM. No speed from another APISQUEEN model was substituted as a U01 specification. Calibration needs a verified RPM curve or measurement.

At 900 visual RPM and above, eight faint swept blade copies add a blur cue so sampled rotation looks less slow or stationary. Geometry inside an 18 mm hub radius is excluded where possible. This adds rendering geometry only and releases its meshes/shared transparent material with the appearance. Sharp blade images may still alias; the effect approximates motion blur.

Validation: 43 rotor checks passed, including six shaft pivots, full 360-degree sweeps inside the housing bores, proportional rotation, stop/blur removal, reverse/derating, topology, lifecycle and reset. Unity compiled. No fresh live SITL trial or standalone build was performed for this change.
