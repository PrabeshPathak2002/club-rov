using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClubROV.Simulation.Editor
{
    public static class RovCourseChecks
    {
        [Serializable] private sealed class Check { public string name; public bool passed; }
        [Serializable] private sealed class Report { public List<Check> checks = new(); }
        public static string Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Run course checks in Play Mode.");
            var course = UnityEngine.Object.FindAnyObjectByType<RovTrainingCourse>();
            var water = UnityEngine.Object.FindAnyObjectByType<RovWaterVisibility>();
            var pilot = UnityEngine.Object.FindAnyObjectByType<RovPilotInput>();
            if (course == null || water == null || pilot == null) throw new InvalidOperationException("Training scene missing components.");
            var report = new Report();
            void Check(string name, bool pass) => report.checks.Add(new Check { name = name, passed = pass });
            bool enabled = pilot.enabled;
            float originalVisibility = water.VisibilityMetres;
            Vector3 cameraPosition = water.transform.position;
            try
            {
                pilot.enabled = false; course.Restart();
                Check("Configured course and idle timer", course.Configured && !course.Started && course.ElapsedSeconds == 0);
                Transform gate1 = GameObject.Find("Training course/Gate 1").transform;
                Transform gate2 = GameObject.Find("Training course/Gate 2").transform;
                Transform gate3 = GameObject.Find("Training course/Gate 3").transform;
                void Pose(Vector3 p, float heading = 0, float seconds = 0.1f, float speed = 0) =>
                    course.EvaluatePose(p, Quaternion.Euler(0, heading, 0), Vector3.forward * speed, seconds);
                void Cross(Transform gate, float x = 0)
                { Pose(gate.TransformPoint(new Vector3(x, 0, -0.5f))); Pose(gate.TransformPoint(new Vector3(x, 0, 0.5f))); }
                Cross(gate2);
                Check("Out-of-order gate rejected", course.GatesPassed == 0);
                course.ResetCourse();
                pilot.GetComponent<RovDynamics>().Body.position = gate1.position + Vector3.forward * 0.5f;
                course.ResetCourse();
                Pose(gate1.position - Vector3.forward * 0.5f);
                Check("Reverse gate crossing rejected", course.GatesPassed == 0);
                course.Restart();
                course.ResetCourse(); Cross(gate1, 1.2f);
                Check("Outside aperture rejected", course.GatesPassed == 0);
                course.ResetCourse(); Cross(gate1); Cross(gate2); Cross(gate3);
                Check("Three ordered forward crossings accepted", course.GatesPassed == 3 && course.Started && course.ElapsedSeconds > 0);
                Vector3 hover = course.TargetPosition;
                Pose(hover, 90, 3.1f);
                Check("Hover wrong heading rejected", !course.HoverComplete && course.HoldSeconds == 0);
                Pose(hover, 0, 3.1f, 0.3f);
                Check("Hover excessive speed rejected", !course.HoverComplete && course.HoldSeconds == 0);
                Pose(hover, 0, 1.5f); Pose(hover + Vector3.right);
                Check("Leaving hover resets continuous hold", course.HoldSeconds == 0 && !course.HoverComplete);
                Pose(hover, 0, 3.1f);
                Check("Steady hover advances to docking", course.HoverComplete && !course.Complete);
                Vector3 dock = course.TargetPosition;
                Pose(dock, 0, 3.1f);
                Check("Dock wrong heading rejected", !course.Complete);
                Pose(dock, 180, 3.1f, 0.2f);
                Check("Dock excessive speed rejected", !course.Complete);
                course.RecordImpact(10); course.RecordImpact(10.1f); course.RecordImpact(10.6f);
                Check("Collision debounce", course.CollisionCount == 2);
                Pose(dock, 180, 3.1f);
                Check("Steady docking completes course", course.Complete);
                float elapsed = course.ElapsedSeconds;
                Pose(dock, 180, 10); course.RecordImpact(20);
                Check("Finished time and collisions freeze", course.ElapsedSeconds == elapsed && course.CollisionCount == 2);
                var document = UnityEngine.Object.FindAnyObjectByType<RovHud>().GetComponent<UIDocument>();
                var root = document.rootVisualElement;
                var slider = root.Q<Slider>("visibility");
                slider.value = 2;
                water.transform.position = new Vector3(0, -1, 0); water.ApplyWater();
                float murky = RenderSettings.fogDensity;
                Check("Live visibility slider affects underwater fog", water.VisibilityMetres == 2 && RenderSettings.fog && murky > 1);
                slider.value = 20; water.ApplyWater();
                Check("Clear water reduces attenuation", RenderSettings.fogDensity < murky / 5);
                water.transform.position = Vector3.up; water.ApplyWater();
                Check("Above-water camera disables underwater fog", !RenderSettings.fog);
                var restart = root.Q<Button>("restart");
                using (var down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, clickCount = 1, mousePosition = restart.worldBound.center })) restart.SendEvent(down);
                using (var up = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, clickCount = 1, mousePosition = restart.worldBound.center })) restart.SendEvent(up);
                Check("HUD restart clears course and vehicle", course.GatesPassed == 0 && !course.Complete && !course.Started && course.CollisionCount == 0 && course.ElapsedSeconds == 0 && pilot.GetComponent<RovDynamics>().Speed == 0);
                Check("Expanded pool dimensions", Mathf.Abs(GameObject.Find("Pool floor").transform.localScale.x - 20.4f) < 0.001f && Mathf.Abs(GameObject.Find("Pool floor").transform.localScale.z - 24.4f) < 0.001f);
            }
            finally
            {
                water.transform.position = cameraPosition; water.SetVisibility(originalVisibility); water.ApplyWater();
                var hud = UnityEngine.Object.FindAnyObjectByType<RovHud>();
                hud.GetComponent<UIDocument>().rootVisualElement.Q<Slider>("visibility").SetValueWithoutNotify(originalVisibility);
                course.Restart(); pilot.enabled = enabled;
            }
            File.WriteAllText(Path.Combine(Application.dataPath, "../CourseCheckResults.json"), JsonUtility.ToJson(report, true));
            int failures = report.checks.FindAll(check => !check.passed).Count;
            if (failures > 0) throw new InvalidOperationException($"{failures} course checks failed; inspect CourseCheckResults.json.");
            return $"{report.checks.Count} course, UI, pool and visibility checks passed.";
        }
    }
}
