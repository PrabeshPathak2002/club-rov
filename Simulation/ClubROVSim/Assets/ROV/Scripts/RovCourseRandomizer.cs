using System;
using UnityEngine;

namespace ClubROV.Simulation
{
    // Owns only the first course's runtime transforms. The saved scene is never modified.
    public sealed class RovCourseRandomizer
    {
        public readonly struct Ranges
        {
            public readonly float Scale, StartLateral, StartDepth, StartHeading;
            public readonly float LayoutLateral, LayoutDepth, LayoutHeading, LayoutLongitudinal;

            public Ranges(Func<string, float, float> parameter)
            {
                Scale = Read(parameter, "course_randomisation", 1, 1);
                float lateral = Read(parameter, "start_lateral_range", 1.5f, 1.5f);
                float depth = Read(parameter, "start_depth_range", 0.3f, 0.3f);
                float heading = Read(parameter, "start_heading_range", 30, 30);
                StartLateral = Mathf.Lerp(Mathf.Min(0.15f, lateral), lateral, Scale);
                StartDepth = Mathf.Lerp(Mathf.Min(0.08f, depth), depth, Scale);
                StartHeading = Mathf.Lerp(Mathf.Min(5, heading), heading, Scale);
                LayoutLateral = Scale * Read(parameter, "layout_lateral_range", 1, 1);
                LayoutDepth = Scale * Read(parameter, "layout_depth_range", 0.4f, 0.4f);
                LayoutHeading = Scale * Read(parameter, "layout_heading_range", 15, 15);
                LayoutLongitudinal = Scale * Read(parameter, "layout_longitudinal_range", 0.75f, 0.75f);
            }

            private static float Read(Func<string, float, float> parameter, string name, float fallback, float maximum)
            {
                float value = parameter(name, fallback);
                if (!float.IsFinite(value)) throw new ArgumentException("Nonfinite RL environment parameter: " + name);
                return Mathf.Clamp(value, 0, maximum);
            }
        }

        private readonly Transform[] targets;
        private readonly Pose[] savedLocalPoses;
        private readonly Vector3[] savedScales;
        private readonly int gateCount;

        public RovCourseRandomizer(RovMapLayout layout)
        {
            if (layout.gates == null || layout.gates.Length != 3 || layout.hover == null || layout.dock == null)
                throw new ArgumentException("RL randomisation requires the three-gate first course.");
            gateCount = layout.gates.Length;
            targets = new Transform[gateCount + 2];
            Array.Copy(layout.gates, targets, gateCount);
            targets[gateCount] = layout.hover;
            targets[gateCount + 1] = layout.dock;
            savedLocalPoses = new Pose[targets.Length];
            savedScales = new Vector3[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null) throw new ArgumentException("RL course target is missing.");
                savedLocalPoses[i] = new Pose(targets[i].localPosition, targets[i].localRotation);
                savedScales[i] = targets[i].localScale;
            }
        }

        public void Randomize(Ranges ranges)
        {
            Restore();
            for (int i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                Vector3 baseline = target.position;
                // Original gate z spacing is >=2.7 m. A +/-0.75 m offset preserves order and clearance.
                Vector3 sampled = new Vector3(
                    Sample(baseline.x, ranges.LayoutLateral, -8.5f, 8.5f),
                    Sample(baseline.y, ranges.LayoutDepth, i < gateCount ? -2.6f : -3.1f,
                        i < gateCount ? -1.05f : -0.6f),
                    Sample(baseline.z, ranges.LayoutLongitudinal, -10.8f, 10.8f));
                float yaw = Sample(0, ranges.LayoutHeading, -15, 15);
                target.SetPositionAndRotation(sampled, Quaternion.AngleAxis(yaw, Vector3.up) * target.rotation);
            }
        }

        private static float Sample(float center, float range, float minimum, float maximum)
        {
            if (range == 0) return center;
            return UnityEngine.Random.Range(Mathf.Max(minimum, center - range), Mathf.Min(maximum, center + range));
        }

        public void Restore()
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null) continue;
                targets[i].SetLocalPositionAndRotation(savedLocalPoses[i].position, savedLocalPoses[i].rotation);
                targets[i].localScale = savedScales[i];
            }
        }
    }
}
