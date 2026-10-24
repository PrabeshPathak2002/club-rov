using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace ClubROV.Simulation
{
    // ArduPilot Sub-4.7.2 libraries/SITL/SIM_JSON.{h,cpp}; little-endian servo packet.
    public static class RovSitlProtocol
    {
        public static bool Decode(byte[] data, int length, ushort[] pwm, out uint frame, out ushort rate)
        {
            frame = 0; rate = 0;
            if (length < 8 || pwm.Length < 6) return false;
            int magic = U16(data, 0);
            if (!((magic == 18458 && length == 40) || (magic == 29569 && length == 72))) return false;
            rate = U16(data, 2);
            // JSON starts at 1200 Hz, then converges toward SCHED_LOOP_RATE.
            if (rate < 50 || rate > 2000) return false;
            frame = (uint)(data[4] | data[5] << 8 | data[6] << 16 | data[7] << 24);
            for (int i = 0; i < 6; i++) pwm[i] = U16(data, 8 + 2 * i);
            return true;
        }
        private static ushort U16(byte[] bytes, int i) => (ushort)(bytes[i] | bytes[i + 1] << 8);
        public static float Pwm(ushort pwm) => pwm < 1000 || pwm > 2000 ? 0 :
            Mathf.Abs(pwm - 1500) <= 25 ? 0 : Mathf.Clamp((pwm - 1500) / 400f, -1, 1);

        // Vectored mixer M1..6 -> measured Unity H1,H2,H3,H4,V1,V2.
        // These signs are simulation conventions, not verified physical motor directions.
        public static void Apply(ushort[] pwm, RovDynamics vehicle)
        {
            vehicle.SetActuator(1, -Pwm(pwm[0]));
            vehicle.SetActuator(0, -Pwm(pwm[1]));
            vehicle.SetActuator(3, Pwm(pwm[2]));
            vehicle.SetActuator(2, Pwm(pwm[3]));
            vehicle.SetActuator(5, -Pwm(pwm[4]));
            vehicle.SetActuator(4, -Pwm(pwm[5]));
        }
        public static Vector3 Polar(Vector3 unity) => new(unity.z, unity.x, -unity.y);
        // Angular velocity is axial: reflection changes its sign relative to polar vectors.
        public static Vector3 Axial(Vector3 unity) => new(-unity.z, -unity.x, unity.y);
        public static Vector3 Euler(Quaternion rotation)
        {
            Vector3 forward = Polar(rotation * Vector3.forward);
            Vector3 right = Polar(rotation * Vector3.right);
            Vector3 down = Polar(rotation * Vector3.down);
            return new(Mathf.Atan2(right.z, down.z), Mathf.Asin(Mathf.Clamp(-forward.z, -1, 1)),
                Mathf.Atan2(forward.y, forward.x));
        }
        public static byte[] Sensors(double time, Vector3 position, Quaternion rotation, Vector3 velocity,
            Vector3 angularVelocity, Vector3 specificForce)
        {
            Quaternion inverse = Quaternion.Inverse(rotation);
            var json = new StringBuilder(512);
            json.Append("\n{\"timestamp\":").Append(time.ToString("F6", CultureInfo.InvariantCulture));
            json.Append(",\"imu\":{\"gyro\":"); Array(json, Axial(inverse * angularVelocity));
            json.Append(",\"accel_body\":"); Array(json, Polar(inverse * specificForce));
            json.Append("},\"position\":"); Array(json, Polar(position));
            json.Append(",\"velocity\":"); Array(json, Polar(velocity));
            json.Append(",\"attitude\":"); Array(json, Euler(rotation));
            json.Append(",\"battery\":{\"voltage\":14.8,\"current\":0},\"no_time_sync\":false,\"no_lockstep\":false}\n");
            return Encoding.ASCII.GetBytes(json.ToString());
        }
        private static void Array(StringBuilder text, Vector3 value)
        {
            text.Append('[').Append(value.x.ToString("F6", CultureInfo.InvariantCulture)).Append(',')
                .Append(value.y.ToString("F6", CultureInfo.InvariantCulture)).Append(',')
                .Append(value.z.ToString("F6", CultureInfo.InvariantCulture)).Append(']');
        }
    }
}
