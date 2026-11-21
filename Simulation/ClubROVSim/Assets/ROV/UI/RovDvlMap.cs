using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClubROV.Simulation
{
    // Receives only ROS local odometry. No vehicle transform or simulation truth is read.
    public sealed class RovDvlMap : VisualElement, IDisposable
    {
        [Serializable] public sealed class Packet
        {
            public int schema;
            public string kind, session, reason;
            public long stamp_ns;
            public float x, y, yaw;
            public bool valid;
        }
        public readonly struct TrackPoint
        {
            public readonly Vector2 Position;
            public readonly bool Break;
            public TrackPoint(Vector2 position, bool gap) { Position = position; Break = gap; }
        }
        public sealed class Track
        {
            public readonly List<TrackPoint> Points = new(512);
            public string Session { get; private set; }
            public string Reason { get; private set; } = "Waiting for ROS odometry";
            public Vector2 Position { get; private set; }
            public float Yaw { get; private set; }
            public bool HasPose { get; private set; }
            private long stamp = -1;
            private double advancedAt = double.NegativeInfinity;
            private bool valid, gap = true;
            public bool Live(double now) => valid && now - advancedAt < 0.75;
            public void Clear() { Points.Clear(); gap = true; }
            public void Reset(string session)
            {
                Session = session; Clear(); HasPose = valid = false; stamp = -1;
                advancedAt = double.NegativeInfinity; Reason = "Waiting for ROS odometry";
            }
            public bool Accept(string json, string expectedSession, double now)
            {
                Packet p;
                try { p = JsonUtility.FromJson<Packet>(json); }
                catch (ArgumentException) { return false; }
                if (p == null || p.schema != 1 || p.kind != "dvl_track" || string.IsNullOrEmpty(expectedSession) ||
                    p.session != expectedSession || p.stamp_ns < 0 || !float.IsFinite(p.x) || !float.IsFinite(p.y) ||
                    !float.IsFinite(p.yaw) || Mathf.Abs(p.x) > 10000 || Mathf.Abs(p.y) > 10000) return false;
                if (Session != p.session)
                {
                    Reset(p.session);
                }
                if (p.stamp_ns < stamp) return false;
                bool advancing = p.stamp_ns > stamp;
                if (advancing) advancedAt = now;
                stamp = p.stamp_ns;
                valid = p.valid;
                Reason = p.reason;
                if (!valid) { gap = true; return true; }
                if (!advancing) return true;
                var next = new Vector2(p.x, p.y);
                if (HasPose && !gap && Vector2.Distance(next, Position) > 2)
                {
                    valid = false; Reason = "Odometry jump: restart ROS"; return true;
                }
                Position = next; Yaw = p.yaw; HasPose = true;
                if (gap || Points.Count == 0 || Vector2.Distance(next, Points[Points.Count - 1].Position) >= 0.05f)
                {
                    if (Points.Count == 512) Points.RemoveAt(0);
                    Points.Add(new TrackPoint(next, gap)); gap = false;
                }
                return true;
            }
            public void MarkStale() { gap = true; }
            public static Vector2 Project(Vector2 flu) => new(-flu.y, -flu.x);
            public static Vector2 Forward(float yaw) => new(-Mathf.Sin(yaw), -Mathf.Cos(yaw));
        }

        private readonly Track track = new();
        private readonly Socket socket;
        private readonly Func<string> sensorSession;
        private readonly Func<bool> streaming;
        private readonly byte[] buffer = new byte[8193];
        private readonly VisualElement plot, body;
        private readonly Label state, scale;
        private readonly Button title;
        private float refresh;
        private bool minimized;
        private float half = 8;
        public Track Data => track;

        public RovDvlMap(VisualElement document, Func<string> session, Func<bool> stream, int port = 9012)
        {
            sensorSession = session; streaming = stream;
            name = "dvl-map"; AddToClassList("dvl-map");
            title = new Button(Toggle) { name = "dvl-map-toggle", text = "−  DVL TRACK", focusable = false,
                tooltip = "DVL/IMU ROS odometry · launch-relative, not GPS or ground truth" };
            Add(title);
            body = new VisualElement(); Add(body);
            var axes = new Label("↑ LAUNCH FORWARD · + = START"); axes.AddToClassList("map-caption"); body.Add(axes);
            plot = new VisualElement { name = "dvl-map-plot", pickingMode = PickingMode.Ignore };
            plot.AddToClassList("map-plot"); plot.generateVisualContent += Draw; body.Add(plot);
            state = new Label("WAITING FOR ROS"); state.AddToClassList("map-caption"); body.Add(state);
            scale = new Label("DVL + IMU estimate"); scale.AddToClassList("map-caption"); body.Add(scale);
            var clear = new Button(() => { track.Clear(); plot.MarkDirtyRepaint(); })
                { name = "dvl-map-clear", text = "CLEAR TRAIL", focusable = false, tooltip = "Clears drawing only; does not reset ROS position" };
            body.Add(clear);
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            try { socket.Bind(new IPEndPoint(IPAddress.Loopback, port)); socket.Blocking = false; }
            catch { socket.Dispose(); throw; }
            document.Q("root").Add(this);
        }
        private void Toggle()
        {
            minimized = !minimized;
            body.style.display = minimized ? DisplayStyle.None : DisplayStyle.Flex;
            title.text = (minimized ? "+  " : "−  ") + "DVL TRACK";
        }
        public void Tick()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (streaming() && track.Session != sensorSession()) track.Reset(sensorSession());
            for (int i = 0; i < 32 && socket.Available > 0; i++)
            {
                int count;
                try { count = socket.Receive(buffer); }
                catch (SocketException) { break; }
                if (count > 8192 || !streaming()) continue;
                track.Accept(Encoding.UTF8.GetString(buffer, 0, count), sensorSession(), now);
            }
            if (Time.unscaledTime < refresh) return;
            refresh = Time.unscaledTime + 0.1f;
            bool live = streaming() && track.Session == sensorSession() && track.Live(now);
            if (!live) track.MarkStale();
            state.text = live ? "LIVE · ROS ODOMETRY" : !streaming() ? "START ROS SENSOR STREAM" :
                !track.HasPose ? "WAITING FOR ROS" : "PAUSED · " + (track.Reason == "live" ? "STALE ODOMETRY" : track.Reason);
            state.EnableInClassList("map-live", live);
            state.EnableInClassList("map-paused", !live);
            if (!minimized)
            {
                half = Bounds(out _);
                string text = $"Grid {half/2:0.#} m · launch-relative";
                if (scale.text != text) scale.text = text;
                plot.MarkDirtyRepaint();
            }
        }
        private float Bounds(out Vector2 center)
        {
            Vector2 min = Vector2.zero, max = Vector2.zero;
            foreach (var point in track.Points)
            {
                var p = Track.Project(point.Position); min = Vector2.Min(min, p); max = Vector2.Max(max, p);
            }
            center = (min + max) * 0.5f;
            return Mathf.Pow(2, Mathf.Ceil(Mathf.Log(Mathf.Max(5, Mathf.Max(max.x - min.x, max.y - min.y) * 0.6f), 2)));
        }
        private void Draw(MeshGenerationContext context)
        {
            float size = Mathf.Min(plot.contentRect.width, plot.contentRect.height) - 24;
            if (size <= 0) return;
            Bounds(out Vector2 center);
            Vector2 origin = new(plot.contentRect.width * 0.5f, plot.contentRect.height * 0.5f);
            Vector2 Screen(Vector2 flu) => origin + (Track.Project(flu) - center) * (size / (half * 2));
            var pen = context.painter2D;
            pen.lineWidth = 1; pen.strokeColor = new Color(.2f,.39f,.43f,.55f);
            for (int i = -2; i <= 2; i++)
            {
                float step = i * size / 4;
                pen.BeginPath(); pen.MoveTo(origin + new Vector2(step,-size/2)); pen.LineTo(origin + new Vector2(step,size/2)); pen.Stroke();
                pen.BeginPath(); pen.MoveTo(origin + new Vector2(-size/2,step)); pen.LineTo(origin + new Vector2(size/2,step)); pen.Stroke();
            }
            var start = Screen(Vector2.zero);
            pen.strokeColor = new Color(1,.83f,.26f); pen.lineWidth = 2;
            pen.BeginPath(); pen.MoveTo(start + Vector2.left*4); pen.LineTo(start + Vector2.right*4);
            pen.MoveTo(start + Vector2.up*4); pen.LineTo(start + Vector2.down*4); pen.Stroke();
            pen.strokeColor = new Color(.3f,.87f,.8f); pen.lineWidth = 2;
            pen.BeginPath();
            for (int i = 0; i < track.Points.Count; i++)
            {
                var p = track.Points[i];
                if (i == 0 || p.Break) pen.MoveTo(Screen(p.Position)); else pen.LineTo(Screen(p.Position));
            }
            pen.Stroke();
            if (track.HasPose && track.Session == sensorSession())
            {
                var p = Screen(track.Position); var forward = Track.Forward(track.Yaw); var side = new Vector2(-forward.y,forward.x);
                bool live = streaming() && track.Live(Time.realtimeSinceStartupAsDouble);
                pen.fillColor = live ? new Color(1,.83f,.26f) : new Color(.65f,.68f,.7f);
                pen.BeginPath(); pen.MoveTo(p + forward*10); pen.LineTo(p-forward*6+side*6);
                pen.LineTo(p-forward*3); pen.LineTo(p-forward*6-side*6); pen.ClosePath(); pen.Fill();
            }
        }
        public void Dispose()
        {
            socket.Dispose(); plot.generateVisualContent -= Draw; RemoveFromHierarchy();
        }
    }
}
