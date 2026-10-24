using UnityEngine;

namespace ClubROV.Simulation
{
    public static class RovThrusterLayout
    {
        public const int Count = 6;
        private static readonly Vector3[] Positions = {
            new(-0.15f, 0, 0.17f), new(0.15f, 0, 0.17f),
            new(-0.15f, 0, -0.17f), new(0.15f, 0, -0.17f),
            new(-0.13f, 0.025f, 0), new(0.13f, 0.025f, 0)
        };
        private static readonly Vector3[] Directions = {
            new Vector3(1, 0, 1).normalized, new Vector3(-1, 0, 1).normalized,
            new Vector3(-1, 0, 1).normalized, new Vector3(1, 0, 1).normalized,
            Vector3.up, Vector3.up
        };

        public static Vector3 Position(int i) => Positions[i];
        public static Vector3 Direction(int i) => Directions[i];

        // Educational layout, not an ArduSub motor-number or propeller convention.
        public static void Mix(Vector3 translation, float yaw, float[] commands)
        {
            float peak = 1;
            for (int i = 0; i < Count; i++)
            {
                float turning = i < 4 ? Mathf.Sign(Vector3.Cross(Positions[i], Directions[i]).y) * yaw : 0;
                commands[i] = Vector3.Dot(Directions[i], translation) + turning;
                peak = Mathf.Max(peak, Mathf.Abs(commands[i]));
            }
            for (int i = 0; i < Count; i++) commands[i] /= peak;
        }
    }
}
