# Pool presentation

`RovWaterVisibility` owns and disposes `RovPoolAppearance`. On enable, the appearance replaces only named pool floor/wall renderer materials and hides the original metre grid. It adds visual water surfaces, deck/coping and a simple stainless ladder, parented to each original pool environment or handbook arena so map activation controls their visibility.

Materials live in `Assets/ROV/Resources/Pool*.mat`. Pool Ceramic draws world-space 20 cm tiles, fine grout, subtle tile variation, a wall waterline band and animated caustic-style light. The original training pool receives dark 2.5 m lane stripes and end crossbars; handbook task arenas retain unmarked tile floors. Pool Surface adds two-sided transparent water with animated shading and grazing-angle highlights. These are artistic effects, not fluid simulation, refraction or physically traced caustics.

Water fog retains its existing 1–20 m visibility control and attenuation curve, with a bluer clear-water tint. No pool dimensions, course objects, vehicle forces, existing collision shapes or sensor measurement code change. Generated decorative objects have no colliders. Disabling the water component restores original materials/grid visibility and removes generated objects/material instances.

Validation: C# compilation passed without errors or warnings; both shader imports reported no messages. Live Game-view inspection covered the underwater tiled floor and lane markings. Three environment detail groups were created under the correct map parents, with zero decorative colliders; disabling removed all three and enabling recreated three. A 10.37-second local-control sample rendered about 114 FPS; SITL pacing and standalone builds were not retested.
