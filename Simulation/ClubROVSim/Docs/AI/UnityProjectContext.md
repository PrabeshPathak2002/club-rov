# ClubROVSim context

Windows desktop ROV training prototype in the existing club-rov repository. Editor 6000.6.7f1, Universal 3D template, URP 17.6.0, Input System 1.20.1. Unity Pipeline 0.9.1-exp.1 was approved for local Editor automation. No second MCP provider is needed.

Baseline: the fresh Editor compiled with zero Console errors and warnings. SampleScene is the untouched template scene. The new PoolTraining scene is generated through the Editor by Assets/ROV/Editor/PoolSceneBuilder.cs. Runtime components live in Assets/ROV/Scripts and UI in Assets/ROV/UI. No networking, ROS, ArduSub SITL or real-hardware connection is implemented.

Use Unity units as metres, +Z forward, +X starboard, +Y up. Source STL uses millimetres with +X forward/+Y port/+Z up; the importer converts and centers it. The September viewing mesh is a visual reference, not calibrated mass/collision geometry. Six thruster positions and hydrodynamic coefficients are provisional and must be measured/tuned.

Apply forces only in FixedUpdate. Separate input from physics so a future SITL bridge can supply six actuator commands. Use UI Toolkit, not OnGUI. No tests framework has been added. Meaningful mixer checks are available via the scene builder; runtime checks must also establish actual movement and reset behavior.

The live Future ROV assembly was measured on 23 October 2026; Docs/FUSION_GEOMETRY.md and Docs/fusion-geometry.json contain its envelope and six analytic thrust-axis lines. This is a geometry input snapshot, not a completed runtime integration. The reference point is geometric, not measured COM. All printed parts use Bambu Lab PETG Basic at 100% infill (corrected from an initial 10% statement). Do not estimate their mass by applying a 10% solid-volume multiplier.

Keep Library, Temp, Logs, UserSettings, generated IDE projects and player builds out of Git. Include Unity-generated .meta files with assets. Do not change firmware or upload simulator assets without a reviewed source set.
