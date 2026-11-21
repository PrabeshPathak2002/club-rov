using System;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClubROV.Simulation
{
    public sealed class RovDebugPanel : IDisposable
    {
        private readonly VisualElement panel;
        private readonly RovDynamics vehicle;
        private readonly RovDvlSensor sensor;
        private readonly RovSitlBridge sitl;
        private readonly Func<RovDvlMap.Track> trackSource;
        private readonly Label clock, imu, dvl, navigation, truth, actuators, environment;
        private readonly Button streamToggle;
        private readonly StringBuilder text = new(512);
        private static readonly string[] ThrusterNames = { "M2 / H1", "M1 / H2", "M4 / H3", "M3 / H4", "M6 / V1", "M5 / V2" };
        private double nextRefresh;

        public RovDebugPanel(VisualElement document, RovDynamics vehicle, Func<RovDvlMap.Track> trackSource)
        {
            this.vehicle = vehicle; this.trackSource = trackSource;
            sensor = vehicle == null ? null : vehicle.GetComponent<RovDvlSensor>();
            sitl = vehicle == null ? null : vehicle.GetComponent<RovSitlBridge>();
            panel = new VisualElement { name = "debug-panel" };
            document.Q<VisualElement>("root").Add(panel);
            var note = new Label("LIVE DIAGNOSTICS · 5 Hz\nSensor vectors: FLU = forward, left, up. Values below are simulated measurements. Start the ROS sensor stream to sample IMU, depth and DVL; opening DEBUG does not start connections.");
            note.AddToClassList("debug-note"); panel.Add(note);
            streamToggle = new Button(ToggleStream) { name = "debug-stream-toggle", text = "START ROS SENSOR STREAM · LOOPBACK", focusable = false };
            streamToggle.SetEnabled(sensor != null); panel.Add(streamToggle);
            var warning = new Label("Stopping or restarting the stream interrupts ROS navigation and creates a new session. Keep it running during an autonomous mission.");
            warning.AddToClassList("debug-note"); panel.Add(warning);
            clock = Section("CONTROL & TIMING");
            imu = Section("IMU & DEPTH · sensor measurements");
            dvl = Section("DVL · latest bottom sample");
            navigation = Section("ROS NAVIGATION · estimator");
            truth = Section("VEHICLE · simulator truth");
            actuators = Section("THRUSTERS · applied output");
            environment = Section("ENVIRONMENT & TETHER · simulator truth");
        }

        private Label Section(string title)
        {
            var heading = new Label(title); heading.AddToClassList("debug-heading"); panel.Add(heading);
            var value = new Label("Waiting for update…"); value.AddToClassList("debug-values"); panel.Add(value);
            return value;
        }

        private static string Vector(Vector3 v) => $"{v.x:+0.000;-0.000;0.000}, {v.y:+0.000;-0.000;0.000}, {v.z:+0.000;-0.000;0.000}";
        private static string Age(RovDvlSensor.Reading sample, bool streaming, double now) =>
            !sample.Available ? "NO SAMPLE" : !streaming ? "STOPPED · last sample" :
            $"{(now - sample.WallTime > 0.75 ? "STALE" : "LIVE")} · age {Math.Max(0, now - sample.WallTime):0.00} s";

        private void ToggleStream()
        {
            if (sensor == null) return;
            if (sitl != null && sitl.Active && (!sitl.TelemetryFresh || sitl.Armed)) return;
            if (sensor.Streaming) sensor.StopStreaming();
            else
            {
                try { sensor.StartStreaming(); }
                catch (Exception error) { imu.text = "Cannot start sensors: " + error.Message; Debug.LogWarning(error.Message); }
            }
        }

        public void Tick(bool visible)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (!visible || now < nextRefresh) return;
            nextRefresh = now + 0.2;
            if (vehicle == null || vehicle.Body == null) { clock.text = "Vehicle unavailable"; return; }
            streamToggle.text = sensor != null && sensor.Streaming ? "STOP ROS SENSOR STREAM" : "START ROS SENSOR STREAM · LOOPBACK";
            streamToggle.SetEnabled(sensor != null && !(sitl != null && sitl.Active && (!sitl.TelemetryFresh || sitl.Armed)));
            clock.text = $"{(sitl == null ? "LOCAL CONTROL" : sitl.ControlState)}\nPhysics owner: {(vehicle.ExternalPhysics ? "SITL" : "Unity local")}\nRender: {1f / Mathf.Max(0.0001f, Time.smoothDeltaTime):0} FPS";
            if (sitl != null)
                clock.text += $"\n{sitl.Status}\nTelemetry: {(sitl.TelemetryFresh ? "fresh" : "unavailable / stale")} · mode {sitl.ControlMode}" +
                    (sitl.Active ? $"\nSimulation {sitl.SimulatedSeconds:0.00} s · {sitl.RealTimeFactor:0.000}× real time\nSteps {sitl.Steps} · {sitl.PhysicsStepsPerWallSecond:0.0}/wall s" : "");
            if (sensor == null) { imu.text = dvl.text = "Sensor component unavailable"; }
            else
            {
                var i = sensor.LatestImu; var d = sensor.LatestDvl;
                imu.text = $"{sensor.Status}\n{Age(i, sensor.Streaming, now)}\nPackets sent: {sensor.PacketsSent}\nSession: {sensor.Session ?? "none"}";
                if (i.Available) imu.text += $"\nSample #{i.Sequence} · sim {i.Seconds:0.000} s\nGyro FLU (rad/s): {Vector(i.Gyro)}\nSpecific force FLU (m/s²): {Vector(i.Acceleration)}\nDepth relative to launch (m): {i.Depth:0.000}\nVariances: gyro {i.GyroVariance:G3}, accel {i.AccelVariance:G3}, depth {i.DepthVariance:G3}";
                dvl.text = Age(d, sensor.Streaming, now);
                if (d.Available) dvl.text += $"\nSample #{d.Sequence} · sim {d.Seconds:0.000} s\n{d.Reason} · {(d.Valid ? "VALID" : "INVALID")} · beams {d.Beams}/3\nAltitude (m): {d.Altitude:0.000}\nVelocity FLU (m/s): {(d.Valid ? Vector(d.Velocity) : "unavailable")}\nVelocity variance: {d.VelocityVariance:G3}";
                dvl.text += $"\nSensor offset FLU (m): {Vector(sensor.SensorOffsetFlu)}";
            }
            var track = trackSource?.Invoke();
            bool matching = sensor != null && sensor.Streaming && track != null && track.Session == sensor.Session;
            navigation.text = track == null ? "Estimator unavailable" :
                $"{(matching && track.Live(now) ? "LIVE" : "WAITING / STALE")} · {track.Reason}\nLaunch-relative FLU frame" +
                (matching && track.HasPose ? $"\nForward {track.Position.x:0.000} m · left {track.Position.y:0.000} m\nYaw {track.Yaw * Mathf.Rad2Deg:0.0}°" : "\nNo current-session pose");
            var body = vehicle.Body;
            truth.text = $"World XYZ position (m): {Vector(body.position)}\nWorld XYZ velocity (m/s): {Vector(body.linearVelocity)}\nWorld XYZ angular velocity (rad/s): {Vector(body.angularVelocity)}\nDepth {vehicle.Depth:0.000} m · speed {vehicle.Speed:0.000} m/s\nHeading {vehicle.Heading:000.0}° · rotation XYZ {Vector(body.rotation.eulerAngles)}";
            text.Clear();
            for (int n = 0; n < 6; n++) text.AppendLine($"{ThrusterNames[n]}: {vehicle.Output(n) * 100:+0.0;-0.0;0.0}% · health {vehicle.ThrusterHealth(n) * 100:0}%");
            actuators.text = text.ToString();
            environment.text = $"Current world XYZ (m/s): {Vector(vehicle.CurrentVelocity)}\nBuoyancy bias {vehicle.BuoyancyBias * 100:+0.0;-0.0;0.0}% · water density {vehicle.WaterDensity:0.0} kg/m³";
            var tether = vehicle.GetComponent<RovTether>();
            if (tether != null) environment.text += $"\n{tether.Status}\nPayout {tether.PaidOutMetres:0.00}/{tether.LengthMetres:0.00} m · distance {tether.DistanceMetres:0.00} m\nTension {tether.TensionNewtons:0.00} N · force world XYZ (N): {Vector(tether.LastForce)}\nAnchor world XYZ (m): {Vector(tether.Anchor)}";
        }

        public void Dispose() => panel.RemoveFromHierarchy();
    }
}
