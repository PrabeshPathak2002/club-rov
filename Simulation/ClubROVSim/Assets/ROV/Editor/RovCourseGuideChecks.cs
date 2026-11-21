using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClubROV.Simulation.Editor
{
    public static class RovCourseGuideChecks
    {
        public static string Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Run in Play Mode.");
            var liveMaps = UnityEngine.Object.FindAnyObjectByType<RovTrainingMaps>();
            var liveVehicle = liveMaps.GetComponent<RovDynamics>();
            int originalMap = liveMaps.Current;
            Vector3 originalPosition = liveVehicle.Body.position;
            var layouts = liveMaps.CopyLayouts();
            var checks = new List<string>();
            void Check(string name, bool pass) { if (!pass) throw new Exception("FAIL: " + name); checks.Add("PASS: " + name); }
            var fixture = new GameObject("Course guide checks - temporary"); fixture.SetActive(false);
            RovCourseGuide guide = null;
            try
            {
                fixture.transform.position = liveMaps.StartPosition(0);
                var dynamics = fixture.AddComponent<RovDynamics>();
                var course = fixture.AddComponent<RovTrainingCourse>(); course.RecordResults = false;
                fixture.SetActive(true);
                void Configure(int map)
                {
                    dynamics.Body.position = liveMaps.StartPosition(map);
                    var layout = layouts[map];
                    course.ConfigureMap(layout.gates, layout.hover, layout.dock, layout.marker, map == 2 || map >= 4);
                }
                int[] counts = { 5, 3, 0, 7, 0, 0 };
                for (int map = 0; map < 6; map++)
                {
                    Configure(map);
                    Check("Map " + map + " has truthful objective count and instructions", course.ObjectiveCount == counts[map]
                        && course.ObjectivesCompleted == 0 && (counts[map] > 0 ? course.PilotInstruction.Length > 20
                            : course.PilotInstruction.Contains("no automatic task completion")));
                }
                Configure(0);
                var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/ROV/UI/RovHud.uxml").CloneTree();
                guide = new RovCourseGuide(root, course, dynamics, null, null);
                void Pose(Vector3 position, float heading = 0, float seconds = 0.1f, float speed = 0)
                {
                    dynamics.Body.position = position; dynamics.Body.rotation = Quaternion.Euler(0, heading, 0);
                    dynamics.Body.linearVelocity = Vector3.forward * speed;
                    course.EvaluatePose(position, dynamics.Body.rotation, dynamics.Body.linearVelocity, seconds);
                    guide.Tick();
                }
                void Cross(Transform gate, float x = 0)
                {
                    Pose(gate.TransformPoint(new Vector3(x, 0, -0.5f)));
                    Pose(gate.TransformPoint(new Vector3(x, 0, 0.5f)));
                }
                Check("Guide and course menu list all five steps before movement", root.Q<Label>("guide-step-4") != null
                    && root.Q<Label>("course-step-4") != null && root.Q<Label>("guide-step-0").text.StartsWith("NOW"));
                Cross(layouts[0].gates[1]);
                Check("Out of order gate does not show progress", course.ObjectivesCompleted == 0 && root.Q<ProgressBar>("course-progress").value == 0);
                Cross(layouts[0].gates[0], 1.2f);
                Check("Missed opening does not show completion", course.ObjectivesCompleted == 0);
                Cross(layouts[0].gates[0]);
                Check("Gate crossing marks done and highlights next step", course.ObjectivesCompleted == 1
                    && root.Q<Label>("guide-step-0").text.StartsWith("DONE") && root.Q<Label>("guide-step-1").text.StartsWith("NOW"));
                Check("Completed objective produces feedback", !root.Q<Label>("guide-toast").ClassListContains("guide-empty")
                    && root.Q<Label>("guide-toast").text.Contains("COMPLETED"));
                course.ResetCourse(); guide.Tick();
                Check("Restart clears checklist and old feedback", course.ObjectivesCompleted == 0
                    && root.Q<Label>("guide-step-0").text.StartsWith("NOW") && root.Q<Label>("guide-toast").ClassListContains("guide-empty"));
                foreach (var gate in layouts[0].gates) Cross(gate);
                Check("All gates lead to cyan hover instructions", course.ObjectivesCompleted == 3 && course.PilotInstruction.Contains("CYAN"));
                var hover = layouts[0].hover;
                Pose(hover.position, hover.eulerAngles.y + 90, 0.5f);
                Check("Hold feedback identifies wrong heading", course.HoldSeconds == 0 && root.Q<Label>("guide-hold").text.Contains("heading align"));
                Pose(hover.position, hover.eulerAngles.y, 0.5f, 0.3f);
                Check("Hold feedback identifies excessive speed", course.HoldSeconds == 0 && root.Q<Label>("guide-hold").text.Contains("slow below 0.15"));
                Pose(hover.position + Vector3.right, hover.eulerAngles.y, 0.5f);
                Check("Hold feedback identifies position outside target", course.HoldSeconds == 0 && root.Q<Label>("guide-hold").text.Contains("move into target"));
                Pose(hover.position, hover.eulerAngles.y, 1.4f);
                Check("Steady hold displays live countdown", course.HoldSeconds > 1.3f && root.Q<Label>("guide-hold").text.Contains("HOLDING 1.4"));
                Pose(hover.position + Vector3.right, hover.eulerAngles.y);
                Check("Leaving target resets continuous hold", course.HoldSeconds == 0 && !course.HoverComplete);
                Pose(hover.position, hover.eulerAngles.y, 3.1f);
                Check("Hover completes one step and advances to green dock", course.ObjectivesCompleted == 4 && course.PilotInstruction.Contains("GREEN"));
                var dock = layouts[0].dock;
                Pose(dock.position, dock.eulerAngles.y, 0.2f, 0.12f);
                Check("Dock uses its stricter speed limit", course.HoldSeconds == 0 && root.Q<Label>("guide-hold").text.Contains("slow below 0.10"));
                Pose(dock.position, dock.eulerAngles.y, 3.1f);
                Check("Course completion marks every step and announces completion", course.Complete && course.ObjectivesCompleted == 5
                    && root.Q<Label>("guide-step-4").text.StartsWith("DONE") && root.Q<Label>("guide-toast").text.Contains("ALL 5"));

                Configure(1); guide.Tick();
                var bar = layouts[1].gates[0]; var pillar = layouts[1].marker;
                Pose(bar.TransformPoint(new Vector3(0, -0.6f, -0.5f)));
                Pose(bar.TransformPoint(new Vector3(0, -0.6f, 0.5f)));
                Check("Pillar course advances from outbound gate to orbit", course.ObjectivesCompleted == 1 && course.PilotInstruction.Contains("Circle the pillar"));
                for (int i = 0; i <= 36; i++)
                {
                    float angle = i * 10 * Mathf.Deg2Rad;
                    Pose(new Vector3(pillar.position.x + 2 * Mathf.Sin(angle), -1.6f, pillar.position.z - 2 * Mathf.Cos(angle)), 0, 0.02f);
                }
                Check("Completed orbit advances to return instructions", course.ObjectivesCompleted == 2 && course.PilotInstruction.Contains("Return UNDER"));
                Pose(bar.TransformPoint(new Vector3(0, -0.6f, 0.5f)));
                Pose(bar.TransformPoint(new Vector3(0, -0.6f, -0.5f)), 180);
                Check("Return gate completes all three pillar objectives", course.Complete && course.ObjectivesCompleted == 3);
                Configure(4); guide.Tick();
                Check("Exploration UI has no fabricated checklist completion", root.Q<Label>("guide-step-0") == null
                    && root.Q<Label>("guide-instruction").text.Contains("no automatic task completion"));
                guide.Dispose(); guide = null;
                Check("Guide cleanup removes card and target cue", root.Q<VisualElement>("course-guide") == null && root.Q<Label>("course-waypoint") == null);
            }
            finally { guide?.Dispose(); UnityEngine.Object.DestroyImmediate(fixture); }
            Check("Validation preserved live vehicle pose and selected map", liveMaps.Current == originalMap && liveVehicle.Body.position == originalPosition);
            return string.Join("\n", checks);
        }
    }
}
