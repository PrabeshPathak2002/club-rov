using UnityEngine;

namespace ClubROV.Simulation
{
    public static class RovThrusterLayout
    {
        public const int Count = 6;
        public static readonly Vector3 EnvelopeMetres = new(0.377472431f, 0.26f, 0.361919731f);
        // Live Fusion snapshot: Docs/fusion-geometry.json. Origin is geometric, not measured COM.
        // H1 front port, H2 front starboard, H3 rear port, H4 rear starboard, V1 port, V2 starboard.
        private static readonly Vector3[] Positions = {
            new(-0.130633733f, -0.016f, 0.133937264f), new(0.127205736f, -0.016f, 0.130509267f),
            new(-0.128919735f, -0.016f, -0.133937264f), new(0.128919735f, -0.016f, -0.133937264f),
            new(-0.152f, -0.014472274f, 0.001566961f), new(0.152f, -0.014472274f, -0.003280959f)
        };
        private static readonly Vector3[] Directions = {
            new Vector3(1, 0, 1).normalized, new Vector3(-1, 0, 1).normalized,
            new Vector3(-1, 0, 1).normalized, new Vector3(1, 0, 1).normalized,
            Vector3.up, Vector3.up
        };
        private static readonly Vector3[] Allocation = CreateAllocation();

        public static Vector3 Position(int i) => Positions[i];
        public static Vector3 Direction(int i) => Directions[i];

        // Minimum-norm allocation for sway, surge and yaw, using measured lever arms.
        // Axis signs remain a simulation convention, not a verified ArduSub mapping.
        public static void Mix(Vector3 translation, float yaw, float[] commands)
        {
            Vector3 wrench = new(2 * translation.x, 2 * translation.z, 4 * yaw);
            float peak = 1;
            for (int i = 0; i < Count; i++)
            {
                commands[i] = i < 4 ? Vector3.Dot(Allocation[i], wrench) : translation.y;
                peak = Mathf.Max(peak, Mathf.Abs(commands[i]));
            }
            for (int i = 0; i < Count; i++) commands[i] /= peak;
        }

        private static Vector3[] CreateAllocation()
        {
            const float yawReferenceMetres = 0.186f;
            var columns = new Vector3[4];
            Vector3 row0 = Vector3.zero, row1 = Vector3.zero, row2 = Vector3.zero;
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = new(Directions[i].x, Directions[i].z,
                    Vector3.Cross(Positions[i], Directions[i]).y / yawReferenceMetres);
                columns[i] = a;
                row0 += a.x * a; row1 += a.y * a; row2 += a.z * a;
            }
            float determinant = Vector3.Dot(row0, Vector3.Cross(row1, row2));
            if (Mathf.Abs(determinant) < 0.00001f)
                throw new System.InvalidOperationException("Thruster layout cannot independently control sway, surge and yaw.");
            Vector3 inverse0 = Vector3.Cross(row1, row2) / determinant;
            Vector3 inverse1 = Vector3.Cross(row2, row0) / determinant;
            Vector3 inverse2 = Vector3.Cross(row0, row1) / determinant;
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = columns[i];
                columns[i] = a.x * inverse0 + a.y * inverse1 + a.z * inverse2;
            }
            return columns;
        }
    }
}
