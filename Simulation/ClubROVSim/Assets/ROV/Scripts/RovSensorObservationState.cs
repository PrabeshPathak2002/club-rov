using UnityEngine;

namespace ClubROV.Simulation
{
    // Policy boundary: only simulated sensor packets and sample time.
    // No vehicle, course, Transform, Rigidbody or target references belong here.
    public static class RovSensorObservationState
    {
        public const int Count = 16;

        public static void Write(RovDvlSensor.Reading imu, RovDvlSensor.Reading dvl,
            double seconds, Vector3 offsetFlu, float[] output)
        {
            if (output.Length != Count)
                throw new System.ArgumentException("Unexpected sensor observation size.");
            System.Array.Clear(output, 0, output.Length);
            double imuAge = imu.Available ? System.Math.Max(0, seconds - imu.Seconds) : 1;
            double dvlAge = dvl.Available ? System.Math.Max(0, seconds - dvl.Seconds) : 1;
            bool imuValid = imu.Available && imuAge <= 0.1 && Finite(imu.Acceleration) && Finite(imu.Gyro) && float.IsFinite(imu.Depth);
            bool dvlValid = dvl.Available && dvl.Valid && dvlAge <= 0.3 && Finite(dvl.Velocity) && Finite(dvl.Gyro);
            if (imuValid)
            {
                Put(output, 0, imu.Acceleration / 20);
                Put(output, 3, imu.Gyro / 2);
                output[6] = Mathf.Clamp01(imu.Depth / 4);
            }
            if (dvlValid)
            {
                // Same FLU lever-arm correction as the ROS measurement consumer.
                Put(output, 7, (dvl.Velocity - Vector3.Cross(dvl.Gyro, offsetFlu)) / 2);
            }
            output[10] = dvl.Available ? Mathf.Clamp01(dvl.Altitude / 4) : 0;
            output[11] = dvlValid ? 1 : 0;
            output[12] = dvl.Available ? Mathf.Clamp01(dvl.Beams / 3f) : 0;
            output[13] = Mathf.Clamp01((float)dvlAge);
            output[14] = Mathf.Clamp01((float)imuAge / 0.2f);
            output[15] = imuValid ? 1 : 0;
            for (int i = 0; i < output.Length; i++) output[i] = FiniteClamp(output[i]);
        }

        private static float FiniteClamp(float value) => float.IsFinite(value) ? Mathf.Clamp(value, -1, 1) : 0;
        private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        private static void Put(float[] output, int start, Vector3 value)
        {
            output[start] = value.x; output[start + 1] = value.y; output[start + 2] = value.z;
        }
    }
}
