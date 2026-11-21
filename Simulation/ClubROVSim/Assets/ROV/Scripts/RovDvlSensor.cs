using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace ClubROV.Simulation
{
    // A configurable measurement model, not a calibrated Tracker 650 emulator.
    [RequireComponent(typeof(RovDynamics))]
    public sealed class RovDvlSensor : MonoBehaviour
    {
        [SerializeField] private bool streamOnStart;
        [SerializeField] private int udpPort = 9010;
        [SerializeField] private Vector3 sensorOffsetFlu = new(-0.000857f, 0, -0.18f);
        [SerializeField, Range(1, 200)] private float imuRateHz = 100;
        [SerializeField, Range(1, 30)] private float dvlRateHz = 10;
        [SerializeField, Min(0)] private float minimumAltitude = 0.5f;
        [SerializeField, Min(1)] private float maximumAltitude = 25;
        [SerializeField, Range(0, 45)] private float maximumTiltDegrees = 20;
        [SerializeField, Min(0)] private float velocityNoiseStd = 0.01f;
        [SerializeField, Min(0)] private float gyroNoiseStd = 0.002f;
        [SerializeField, Min(0)] private float accelNoiseStd = 0.02f;
        [SerializeField, Min(0)] private float depthNoiseStd = 0.005f;
        [SerializeField, Range(0, 1)] private float dropoutProbability;
        [SerializeField] private bool includeGroundTruth;
        [SerializeField] private int randomSeed = 650;

        private RovDynamics vehicle;
        private Socket socket;
        private IPEndPoint destination;
        private System.Random random;
        private string session;
        private long sequence;
        private double time, nextImu, nextDvl;
        private Vector3 launchPosition, previousVelocity;
        private Quaternion launchHeading;
        private bool localSampling;
        private int localSeed, localSession;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        public bool Streaming => socket != null;
        public string Session => session;
        public string Status { get; private set; } = "ROS sensors stopped";
        public long PacketsSent { get; private set; }
        public Vector3 SensorOffsetFlu => sensorOffsetFlu;
        public Reading LatestImu { get; private set; }
        public Reading LatestDvl { get; private set; }
        public double SampleSeconds => time;
        public bool LocalSampling => localSampling;
        // Reuse the measurement model without opening UDP ports in parallel RL players.
        // Local pressure is zeroed at the pool surface (y=0); ROS retains its launch-relative depth.
        public void StartLocalSampling(int seed)
        {
            if (Streaming || localSampling) throw new InvalidOperationException("Sensor already has a sampling owner.");
            localSampling = true; localSeed = seed; localSession = 0;
            ResetSession();
        }
        public void StopLocalSampling()
        {
            localSampling = false;
            LatestImu = LatestDvl = default;
        }
        public readonly struct Reading
        {
            public readonly bool Available, Valid;
            public readonly long Sequence;
            public readonly double Seconds, WallTime;
            public readonly Vector3 Gyro, Acceleration, Velocity;
            public readonly float Depth, Altitude, GyroVariance, AccelVariance, DepthVariance, VelocityVariance;
            public readonly int Beams;
            public readonly string Reason;
            public Reading(Packet p)
            {
                Available = true; Valid = p.valid; Sequence = p.seq; Seconds = p.stamp_ns / 1e9;
                WallTime = Time.realtimeSinceStartupAsDouble;
                Gyro = new Vector3(p.gyro[0], p.gyro[1], p.gyro[2]);
                Acceleration = new Vector3(p.accel[0], p.accel[1], p.accel[2]);
                Velocity = new Vector3(p.velocity[0], p.velocity[1], p.velocity[2]);
                Depth = p.depth; Altitude = p.altitude; Beams = p.beams; Reason = p.reason;
                GyroVariance = p.gyro_variance; AccelVariance = p.accel_variance;
                DepthVariance = p.depth_variance; VelocityVariance = p.velocity_variance;
            }
        }

        [Serializable] public sealed class Packet
        {
            public int schema = 1;
            public string session;
            public long seq, stamp_ns;
            public float[] gyro, accel, dvl_offset;
            public float gyro_variance, accel_variance, depth, depth_variance;
            public bool has_dvl, valid, has_truth;
            public int beams;
            public float altitude, velocity_variance;
            public float[] velocity;
            public string reason;
            public Truth truth;
        }
        [Serializable] public sealed class Truth
        {
            public float[] position, orientation, velocity, angular_velocity;
        }

        private void Awake() => vehicle = GetComponent<RovDynamics>();
        private void OnEnable()
        {
            if (vehicle == null) vehicle = GetComponent<RovDynamics>();
            vehicle.PhysicsStepped += Step;
            vehicle.VehicleReset += ResetSession;
        }
        private void Start() { if (streamOnStart) StartStreaming(); }
        private void OnDisable()
        {
            vehicle.PhysicsStepped -= Step;
            vehicle.VehicleReset -= ResetSession;
            StopStreaming();
            StopLocalSampling();
        }
        [ContextMenu("Start ROS sensor stream (loopback UDP 9010)")]
        public void StartStreaming()
        {
            if (localSampling) throw new InvalidOperationException("Stop local RL sampling before starting ROS streaming.");
            if (Streaming) return;
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            if (udpPort < 1024 || udpPort > 65535) throw new ArgumentOutOfRangeException(nameof(udpPort));
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            destination = new IPEndPoint(IPAddress.Loopback, udpPort);
            ResetSession();
            Status = "ROS sensors started · waiting for physics step";
        }
        [ContextMenu("Stop ROS sensor stream")]
        public void StopStreaming()
        {
            socket?.Dispose(); socket = null;
            Status = "ROS sensors stopped";
        }
        private void ResetSession()
        {
            if (vehicle == null || vehicle.Body == null) return;
            session = Guid.NewGuid().ToString("N");
            sequence = 0; time = nextImu = nextDvl = 0; PacketsSent = 0;
            LatestImu = LatestDvl = default;
            random = new System.Random(localSampling ? unchecked(localSeed + 7919 * localSession++) : randomSeed);
            launchPosition = vehicle.Body.position;
            launchHeading = Quaternion.Euler(0, vehicle.Body.rotation.eulerAngles.y, 0);
            previousVelocity = vehicle.Body.linearVelocity;
        }
        public static Vector3 PolarFlu(Vector3 unity) => new(unity.z, -unity.x, unity.y);
        public static Vector3 AxialFlu(Vector3 unity) => -PolarFlu(unity);
        public static Vector3 FromFlu(Vector3 flu) => new(-flu.y, flu.z, flu.x);
        public static Quaternion RotationFlu(Quaternion unity) => new(-unity.z, unity.x, -unity.y, unity.w);
        public static Vector3 VelocityAtSensor(Vector3 velocity, Vector3 angularVelocity,
            Quaternion rotation, Vector3 offsetFlu) => PolarFlu(Quaternion.Inverse(rotation) *
                (velocity + Vector3.Cross(angularVelocity, rotation * FromFlu(offsetFlu))));

        private void Step(float dt)
        {
            if ((!Streaming && !localSampling) || dt <= 0) return;
            time += dt;
            Vector3 acceleration = (vehicle.Body.linearVelocity - previousVelocity) / dt;
            previousVelocity = vehicle.Body.linearVelocity;
            if (time + 1e-8 < nextImu) return;
            nextImu = time + 1.0 / Mathf.Max(1, imuRateHz);
            var body = vehicle.Body;
            Quaternion inverse = Quaternion.Inverse(body.rotation);
            bool dvlDue = time + 1e-8 >= nextDvl;
            if (dvlDue) nextDvl = time + 1.0 / Mathf.Max(1, dvlRateHz);
            var packet = new Packet {
                session = session, seq = sequence++, stamp_ns = (long)Math.Round(time * 1e9),
                gyro = Array(Noisy(AxialFlu(inverse * body.angularVelocity), gyroNoiseStd)),
                accel = Array(Noisy(PolarFlu(inverse * (acceleration - Physics.gravity)), accelNoiseStd)),
                gyro_variance = Mathf.Max(1e-10f, gyroNoiseStd * gyroNoiseStd),
                accel_variance = Mathf.Max(1e-10f, accelNoiseStd * accelNoiseStd),
                depth = (localSampling ? 0 : launchPosition.y) - body.position.y + Gaussian() * depthNoiseStd,
                depth_variance = Mathf.Max(1e-10f, depthNoiseStd * depthNoiseStd),
                dvl_offset = Array(sensorOffsetFlu), has_dvl = dvlDue, has_truth = includeGroundTruth,
                velocity = new float[3], reason = "not_due",
                velocity_variance = Mathf.Max(1e-10f, velocityNoiseStd * velocityNoiseStd)
            };
            if (dvlDue)
            {
                SampleBottom(out int beams, out float altitude, out string reason);
                packet.beams = beams; packet.altitude = altitude; packet.reason = reason;
                packet.valid = reason == "bottom_lock";
                if (packet.valid)
                    packet.velocity = Array(Noisy(VelocityAtSensor(body.linearVelocity, body.angularVelocity,
                        body.rotation, sensorOffsetFlu), velocityNoiseStd));
            }
            if (includeGroundTruth)
            {
                Quaternion relative = Quaternion.Inverse(launchHeading) * body.rotation;
                Quaternion q = RotationFlu(relative);
                packet.truth = new Truth {
                    position = Array(PolarFlu(Quaternion.Inverse(launchHeading) * (body.position - launchPosition))),
                    orientation = new[] { q.x, q.y, q.z, q.w },
                    velocity = Array(PolarFlu(inverse * body.linearVelocity)),
                    angular_velocity = Array(AxialFlu(inverse * body.angularVelocity))
                };
            }
            LatestImu = new Reading(packet);
            if (dvlDue) LatestDvl = LatestImu;
            if (!Streaming) return;
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
                socket.SendTo(data, destination); PacketsSent++;
                Status = $"ROS sensors · {PacketsSent} packets · {packet.reason}";
            }
            catch (SocketException error)
            {
                StopStreaming(); Status = "ROS sensor socket: " + error.SocketErrorCode;
                Debug.LogWarning(Status);
            }
        }
        private void SampleBottom(out int beams, out float altitude, out string reason)
        {
            beams = 0; altitude = 0;
            var body = vehicle.Body;
            if (Vector3.Angle(body.rotation * Vector3.down, Vector3.down) > maximumTiltDegrees)
            { reason = "tilt"; return; }
            Vector3 origin = body.position + body.rotation * FromFlu(sensorOffsetFlu);
            float cosine = Mathf.Cos(20 * Mathf.Deg2Rad);
            for (int beam = 0; beam < 3; beam++)
            {
                float az = beam * 120 * Mathf.Deg2Rad;
                Vector3 flu = new(Mathf.Sin(20 * Mathf.Deg2Rad) * Mathf.Cos(az),
                    Mathf.Sin(20 * Mathf.Deg2Rad) * Mathf.Sin(az), -cosine);
                Vector3 direction = body.rotation * FromFlu(flu);
                int count = Physics.RaycastNonAlloc(origin, direction, hits, maximumAltitude / cosine,
                    ~0, QueryTriggerInteraction.Ignore);
                float nearest = float.PositiveInfinity;
                for (int i = 0; i < count; i++)
                    if (!hits[i].transform.IsChildOf(transform) && hits[i].distance < nearest)
                        nearest = hits[i].distance;
                // Saturated buffers are ambiguous; do not invent a bottom lock.
                if (count == hits.Length || float.IsInfinity(nearest)) continue;
                float height = nearest * cosine;
                if (height < minimumAltitude || height > maximumAltitude) continue;
                // Require a floor-like surface at the nearest return, not a wall behind it.
                bool floor = false;
                for (int i = 0; i < count; i++)
                    if (Mathf.Abs(hits[i].distance - nearest) < 1e-4f && Vector3.Dot(hits[i].normal, Vector3.up) > 0.7f)
                        floor = true;
                if (floor) { beams++; altitude += height; }
            }
            altitude = beams > 0 ? altitude / beams : 0;
            reason = beams != 3 ? "no_bottom_lock" : random.NextDouble() < dropoutProbability ? "dropout" : "bottom_lock";
        }
        private float Gaussian() => (float)(Math.Sqrt(-2 * Math.Log(Math.Max(1e-12, random.NextDouble()))) *
            Math.Cos(2 * Math.PI * random.NextDouble()));
        private Vector3 Noisy(Vector3 value, float sigma) => value + sigma * new Vector3(Gaussian(), Gaussian(), Gaussian());
        private static float[] Array(Vector3 v) => new[] { v.x, v.y, v.z };
    }
}
