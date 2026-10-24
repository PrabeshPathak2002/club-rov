using System;
using System.Collections.Generic;
using System.IO;
using ClubROV.Simulation;
using UnityEditor;
using UnityEngine;

namespace ClubROV.Simulation.Editor
{
    public static class RovSmokeChecks
    {
        [Serializable] private sealed class Result
        {
            public string name;
            public bool passed;
            public string observation;
        }
        [Serializable] private sealed class Report
        {
            public bool complete;
            public List<Result> checks = new();
        }
        private static Report report;
        private static RovDynamics vehicle;
        private static RovPilotInput input;
        private static float started;
        private static Vector3 position;
        private static float heading;
        private static int stage;
        private static readonly string[] Names = { "Neutral", "Forward", "Reverse", "Strafe", "Yaw", "Ascend", "Thrust cut", "Floor collision", "Reset" };
        private static readonly Vector3[] Translations = { Vector3.zero, Vector3.forward, Vector3.back, Vector3.right, Vector3.zero, Vector3.up, Vector3.forward, Vector3.down, Vector3.zero };

        public static string Start()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Run smoke checks in Play Mode.");
            if (vehicle != null) throw new InvalidOperationException("Checks are already running.");
            vehicle = UnityEngine.Object.FindAnyObjectByType<RovDynamics>();
            if (vehicle == null) throw new InvalidOperationException("Open PoolTraining first.");
            input = vehicle.GetComponent<RovPilotInput>();
            input.enabled = false;
            report = new Report(); stage = 0;
            BeginStage();
            EditorApplication.update += Tick;
            return "Smoke checks started; report will be written to SmokeCheckResults.json in the project root.";
        }

        private static void BeginStage()
        {
            vehicle.ResetVehicle();
            if (stage == 7) vehicle.Body.position = new Vector3(0, -3.0f, -1);
            position = vehicle.Body.position;
            heading = vehicle.Body.rotation.eulerAngles.y;
            started = Time.fixedTime;
            vehicle.SetPilotCommand(Translations[stage], stage == 4 ? 1 : 0);
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || vehicle == null) { Finish(false); return; }
            if (Time.fixedTime - started < 0.8f) return;
            Vector3 delta = vehicle.Body.position - position;
            float turn = Mathf.DeltaAngle(heading, vehicle.Body.rotation.eulerAngles.y);
            bool passed = false;
            switch (stage)
            {
                case 0: passed = delta.magnitude < 0.01f && vehicle.Speed < 0.01f; break;
                case 1: passed = delta.z > 0.05f && Mathf.Abs(delta.x) < 0.02f; break;
                case 2: passed = delta.z < -0.05f; break;
                case 3: passed = delta.x > 0.05f; break;
                case 4: passed = turn > 3 && new Vector2(delta.x, delta.z).magnitude < 0.02f; break;
                case 5: passed = delta.y > 0.05f; break;
                case 6:
                    vehicle.StopThrusters(); passed = true;
                    for (int i = 0; i < 6; i++) passed &= Mathf.Abs(vehicle.Output(i)) < 0.0001f;
                    break;
                case 7: passed = vehicle.GetComponent<Collider>().bounds.min.y >= -3.51f && vehicle.Body.linearVelocity.y > -0.05f; break;
                case 8:
                    vehicle.ResetVehicle();
                    passed = Vector3.Distance(vehicle.Body.position, new Vector3(0, -1.2f, -1)) < 0.001f && vehicle.Speed < 0.001f;
                    break;
            }
            report.checks.Add(new Result { name = Names[stage], passed = passed, observation = $"delta={delta:F3}, yaw={turn:F2}, speed={vehicle.Speed:F3}, frame={Time.frameCount}" });
            stage++;
            if (stage == Names.Length) Finish(true); else BeginStage();
        }

        private static void Finish(bool complete)
        {
            EditorApplication.update -= Tick;
            if (report != null)
            {
                report.complete = complete;
                File.WriteAllText(Path.Combine(Application.dataPath, "../SmokeCheckResults.json"), JsonUtility.ToJson(report, true));
            }
            if (vehicle != null) vehicle.ResetVehicle();
            if (input != null) input.enabled = true;
            vehicle = null; input = null;
        }
    }
}
