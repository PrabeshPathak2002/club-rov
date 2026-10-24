# RoboSub 2026 training arenas

I used the [July 11 handbook](https://robonation.org/app/uploads/sites/4/2026/07/RoboSub-2026_Team-Handbook_20260711-compressed.pdf), pp. 80 and 82, for the practice and finals arrangements. The finals variant combines A/B1/B2; the other fields remain alongside it. The diagrams explicitly say they are not to scale. Normalized task centers are recorded in `robosub-2026-layout.json` and mirrored by `RoboSub2026Setup`; these are traced approximations, not measured task coordinates.

The venue footprint is 50 m × 25 yd (22.86 m), per [NOVA's facility description](https://www.gomotionapp.com/team/nova/page/locations/woollett-aquatic-center). Depth is 2.1 m, per handbook p. 72. The four earlier maps retain their original environment and depth.

Task geometry uses the drawing units: 3.048 m gate span, 1.524 m legs, 0.6096 m divider; 0.9144 m debris poles; 0.6096 m torpedo board; 2.7432 m octagon; 1.2192 × 0.1524 m path segments. Only the gate-to-slalom and slalom-to-bins paths are included, following the PDF p. 54 rather than the less specific online text.

Unspecified depths, pipe thicknesses, crate/basket details, torpedo opening sizes, and station orientation are practice estimates. Role graphics use simple colored panels and S&R/SOS labels; they are not the official visual-recognition artwork. Pingers are static location markers. No torpedo firing, marker drops, magnetic interactions, acoustic sensor or object handling is implemented. These maps are unscored exploration layouts; they do not award official competition points.

Choose **RoboSub 2026 practice** or **RoboSub 2026 finals** in Training map, then Load Map before connecting SITL. Conditions presets preserve that map. The start is just submerged at the A/AB launch, rather than simulating the surface launch (partial-submergence buoyancy is not calibrated). Restart SITL after changing maps/origins. Starts saved in these arenas are limited to 0.2–1.85 m depth and their own pool bounds.
