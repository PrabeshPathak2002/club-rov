using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClubROV.Simulation
{
    [Serializable]
    public sealed class RovPilotFeedback
    {
        // Zero denotes an older saved run without feedback (JsonUtility constructs missing classes).
        public int version;
        public bool completed, qualified, scoredCourse;
        public int map;
        public float distance, lowSpeedThrustSeconds;
        public float[] meanThrust = new float[6], peakThrust = new float[6];
        public int[] reversals = new int[6];
    }

    public sealed class RovRunReview
    {
        public RovRunResult result, previous;
        public Vector3[] route, impacts;
        public int totalImpacts;
    }

    // Collect on the simulator's physics clock, independently of render FPS.
    public sealed class RovRunRecorder
    {
        public const int MaxRoutePoints = 512, MaxImpactPoints = 128;
        private readonly List<Vector3> route = new(), impacts = new();
        private readonly double[] thrustIntegral = new double[6];
        private readonly float[] peaks = new float[6];
        private readonly int[] reversals = new int[6], signs = new int[6];
        private Vector3 previousPosition;
        private double seconds, lowSpeedSeconds, distance;
        private double nextRouteTime, routeInterval = 0.2;
        public bool Started { get; private set; }
        public float Seconds => (float)seconds;
        public int TotalImpacts { get; private set; }

        public void Reset(Vector3 position)
        {
            route.Clear(); impacts.Clear();
            Array.Clear(thrustIntegral, 0, 6); Array.Clear(peaks, 0, 6);
            Array.Clear(reversals, 0, 6); Array.Clear(signs, 0, 6);
            seconds = lowSpeedSeconds = distance = nextRouteTime = 0;
            routeInterval = 0.2; Started = false; TotalImpacts = 0;
            previousPosition = position;
        }

        public void Sample(Vector3 position, Vector3 velocity, float dt, float[] outputs, bool allowStart = true)
        {
            if (!(dt > 0) || !float.IsFinite(dt) || !Finite(position) || !Finite(velocity)) return;
            if (!Started)
            {
                if (!allowStart) { previousPosition = position; return; }
                if (velocity.magnitude <= 0.05f && Vector3.Distance(position, previousPosition) <= 0.05f) return;
                Started = true;
                route.Add(previousPosition);
            }
            seconds += dt;
            distance += Vector3.Distance(position, previousPosition);
            previousPosition = position;
            bool thrusting = false;
            for (int i = 0; i < 6; i++)
            {
                float value = float.IsFinite(outputs[i]) ? Mathf.Clamp(outputs[i], -1, 1) : 0;
                float magnitude = Mathf.Abs(value);
                thrustIntegral[i] += magnitude * dt;
                peaks[i] = Mathf.Max(peaks[i], magnitude);
                int sign = magnitude >= 0.05f ? (value > 0 ? 1 : -1) : 0;
                if (sign != 0)
                {
                    if (signs[i] != 0 && signs[i] != sign) reversals[i]++;
                    signs[i] = sign;
                }
                if (i < 4 && magnitude >= 0.1f) thrusting = true;
            }
            if (thrusting && velocity.magnitude < 0.15f) lowSpeedSeconds += dt;
            if (seconds >= nextRouteTime)
            {
                AddRoutePoint(position);
                nextRouteTime = seconds + routeInterval;
            }
        }

        private static bool Finite(Vector3 p) => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);

        private void AddRoutePoint(Vector3 point)
        {
            if (route.Count >= MaxRoutePoints)
            {
                // Decimate the entire trace rather than discarding the beginning of a long run.
                int write = 1;
                for (int read = 2; read < route.Count; read += 2) route[write++] = route[read];
                route.RemoveRange(write, route.Count - write);
                routeInterval *= 2;
            }
            route.Add(point);
        }

        public void Impact(Vector3 position)
        {
            if (!Started || !Finite(position)) return;
            TotalImpacts++;
            if (impacts.Count < MaxImpactPoints) impacts.Add(position);
        }

        public RovRunReview Snapshot(RovRunResult result, RovRunResult previous, bool completed, bool qualified, bool scored, int map)
        {
            var feedback = new RovPilotFeedback { version = 1, completed = completed, qualified = qualified, scoredCourse = scored,
                map = map, distance = (float)distance, lowSpeedThrustSeconds = (float)lowSpeedSeconds };
            for (int i = 0; i < 6; i++)
            {
                feedback.meanThrust[i] = seconds > 0 ? (float)(thrustIntegral[i] / seconds) : 0;
                feedback.peakThrust[i] = peaks[i]; feedback.reversals[i] = reversals[i];
            }
            result.feedback = feedback;
            // Always retain the final pose even between scheduled samples.
            var path = new List<Vector3>(route);
            if (Started && (path.Count == 0 || path[path.Count - 1] != previousPosition))
            {
                if (path.Count == MaxRoutePoints) path.RemoveAt(path.Count - 1);
                path.Add(previousPosition);
            }
            return new RovRunReview { result = result, previous = previous,
                route = path.ToArray(), impacts = impacts.ToArray(), totalImpacts = TotalImpacts };
        }
    }
}
