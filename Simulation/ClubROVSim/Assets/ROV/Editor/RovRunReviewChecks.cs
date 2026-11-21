using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClubROV.Simulation.Editor
{
    public static class RovRunReviewChecks
    {
        public static string Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Run in Play Mode.");
            var checks = new List<string>();
            void Check(string name, bool passed) { if (!passed) throw new Exception("FAIL: " + name); checks.Add("PASS: " + name); }
            bool Near(float a, float b) => Mathf.Abs(a - b) < 0.001f;
            string historyPath = Path.Combine(Application.persistentDataPath, "training-runs.json");
            string beforeHistory = File.Exists(historyPath) ? File.ReadAllText(historyPath) : null;
            var recorder = new RovRunRecorder(); recorder.Reset(Vector3.zero);
            var outputs = new float[6];
            recorder.Sample(Vector3.zero, Vector3.zero, 1, outputs);
            Check("Idle time excluded", !recorder.Started && recorder.Seconds == 0);
            for (int i = 0; i < 4; i++)
            {
                outputs[0] = i == 2 ? 0 : i == 1 ? -0.5f : 0.5f;
                recorder.Sample(new Vector3((i + 1) * 0.05f, 0, 0), Vector3.right * 0.1f, 0.5f, outputs);
            }
            var snapshot = recorder.Snapshot(new RovRunResult { seconds = recorder.Seconds }, null, false, true, false, 2);
            Check("Physics-time weighted thrust average and peak", Near(snapshot.result.feedback.meanThrust[0], 0.375f) && Near(snapshot.result.feedback.peakThrust[0], 0.5f));
            Check("Direction reversals survive neutral interval", snapshot.result.feedback.reversals[0] == 2);
            Check("Low speed thrust proxy duration", Near(snapshot.result.feedback.lowSpeedThrustSeconds, 1.5f));
            Check("Travel distance and endpoint", Near(snapshot.result.feedback.distance, 0.2f) && Near(snapshot.route[snapshot.route.Length - 1].x, 0.2f));
            recorder.Sample(Vector3.one, Vector3.one, float.NaN, outputs);
            recorder.Sample(new Vector3(float.NaN, 0, 0), Vector3.one, 1, outputs);
            Check("Nonfinite samples rejected", Near(recorder.Seconds, 2));
            recorder.Reset(Vector3.zero);
            Check("Snapshot survives recorder reset", snapshot.route.Length > 1 && Near(snapshot.result.feedback.meanThrust[0], 0.375f));
            for (int i = 0; i < 20000; i++) recorder.Sample(new Vector3(i * 0.01f, 0, 0), Vector3.right, 0.2f, outputs);
            for (int i = 0; i < 200; i++) recorder.Impact(Vector3.right * i);
            var longRun = recorder.Snapshot(new RovRunResult(), null, true, true, true, 0);
            Check("Long route bounded, beginning and endpoint retained", longRun.route.Length <= RovRunRecorder.MaxRoutePoints && longRun.route[0] == Vector3.zero && Near(longRun.route[longRun.route.Length - 1].x, 199.99f));
            Check("Impact markers bounded but total retained", longRun.impacts.Length == RovRunRecorder.MaxImpactPoints && longRun.totalImpacts == 200);

            var history = JsonUtility.FromJson<RovRunFile>("{\"version\":1,\"runs\":[{\"scenarioKey\":\"old\",\"score\":400}],\"bestRuns\":[]}");
            Check("Legacy history readable without invented feedback", history.runs[0].feedback == null || history.runs[0].feedback.version == 0);
            var previous = new RovRunResult { scenarioKey = "match", score = 650, feedback = new RovPilotFeedback { version = 1, qualified = true } };
            history.runs.Add(previous);
            history.runs.Add(new RovRunResult { scenarioKey = "other", score = 900 });
            Check("Comparison uses matching key rather than latest unrelated run", RovTrainingData.PreviousReview(history, "match", true) == previous && RovTrainingData.PreviousReview(history, "missing", true) == null);
            for (int i = 0; i < 25; i++) history = RovTrainingData.AppendPracticeReview(history, new RovRunResult { scenarioKey = "practice", seconds = i, feedback = new RovPilotFeedback { version = 1, qualified = true } });
            Check("Practice history bounded separately from score history", history.practiceReviews.Count == 20 && history.runs.Count == 3 && history.bestRuns.Count == 0);
            Check("Completed and practice comparisons separated", RovTrainingData.PreviousReview(history, "practice", true) == null && RovTrainingData.PreviousReview(history, "practice", false).seconds == 24);
            var roundTrip = JsonUtility.FromJson<RovRunFile>(JsonUtility.ToJson(history));
            Check("Feedback metadata survives JSON round trip", RovTrainingData.PreviousReview(roundTrip, "practice", false).feedback.version == 1);
            var maximum = new RovRunFile();
            for (int i = 0; i < 256; i++) maximum.bestRuns.Add(previous);
            for (int i = 0; i < 100; i++) maximum.runs.Add(previous);
            for (int i = 0; i < 20; i++) maximum.practiceReviews.Add(previous);
            Check("Bounded extended history stays below existing 1 MB cap", System.Text.Encoding.UTF8.GetByteCount(JsonUtility.ToJson(maximum, true)) < 1024 * 1024);

            var fixture = new GameObject("Run review checks - temporary"); fixture.SetActive(false);
            try
            {
                var vehicle = new GameObject("Test vehicle"); vehicle.transform.SetParent(fixture.transform); vehicle.transform.position = new Vector3(0, -1, -1);
                var dynamics = vehicle.AddComponent<RovDynamics>();
                var course = vehicle.AddComponent<RovTrainingCourse>(); course.RecordResults = false;
                Transform Target(string name, Vector3 position, float heading = 0)
                {
                    var go = new GameObject(name); go.transform.SetParent(fixture.transform); go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, heading, 0)); return go.transform;
                }
                var gate = Target("Gate", new Vector3(0, -1, 0));
                var hover = Target("Hover", new Vector3(0, -1, 2));
                var dock = Target("Dock", new Vector3(0, -1, 3), 180);
                course.Configure(new[] { gate }, hover, dock);
                fixture.SetActive(true); course.SetRunConditions("review-check-isolated", "Validation only");
                for (int i = 1; i <= 10; i++) course.EvaluatePose(new Vector3(0, -1, -1 + i * 0.01f), Quaternion.identity, Vector3.forward * 0.01f, 0.02f);
                Check("Course and recorder agree on idle slow drift", !course.Started && !course.CanEndPractice && course.RecordedSeconds == 0);
                course.ResetCourse();
                int ready = 0; course.ReviewReady += _ => ready++;
                course.EvaluatePose(new Vector3(0, -1, -0.5f), Quaternion.identity, Vector3.forward, 0.1f);
                course.EvaluatePose(new Vector3(0, -1, 0.5f), Quaternion.identity, Vector3.forward, 0.1f);
                course.EvaluatePose(hover.position, Quaternion.identity, Vector3.zero, 3.1f);
                course.EvaluatePose(dock.position, dock.rotation, Vector3.zero, 3.1f);
                Check("Course completion publishes one complete review", course.Complete && ready == 1 && course.LastReview.result.feedback.completed);
                Check("Review uses existing depth and heading metrics", Near(course.LastReview.result.depthRms, course.DepthRms) && Near(course.LastReview.result.headingRms, course.HeadingRms));
                float seconds = course.RecordedSeconds;
                course.EvaluatePose(Vector3.one, Quaternion.identity, Vector3.one, 10);
                course.EndPracticeReview();
                Check("Completed recording frozen and no duplicate review", Near(course.RecordedSeconds, seconds) && ready == 1);
                course.ConfigureMap(Array.Empty<Transform>(), null, null, null, true);
                course.EvaluatePose(new Vector3(0, -1, -0.8f), Quaternion.identity, Vector3.forward, 0.1f);
                course.RecordImpact(0.1f, Vector3.one); course.RecordImpact(0.2f, Vector3.one); course.RecordImpact(0.6f, Vector3.one * 2);
                course.EndPracticeReview();
                Check("Free swim can finish and record debounced collision positions", course.ReviewEnded && !course.Complete && !course.LastReview.result.feedback.scoredCourse && course.LastReview.impacts.Length == 2 && course.LastReview.result.collisions == 2);
                Check("Practice has no score and cannot finish twice", course.LastReview.result.score == 0 && !course.CanEndPractice);
                var latest = course.LastReview; course.ResetCourse();
                Check("Restart clears recording while keeping last review available", !course.CanEndPractice && course.RecordedSeconds == 0 && course.LastReview == latest);
                course.EvaluatePose(new Vector3(0, -1, -0.7f), Quaternion.identity, Vector3.forward, 0.1f);
                course.SetRunConditions("changed", "Changed conditions"); course.EndPracticeReview();
                Check("Conditions changed in free swim invalidate comparison", !course.LastReview.result.feedback.qualified && course.LastReview.previous == null);
                var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/ROV/UI/RovHud.uxml");
                var root = asset.CloneTree();
                var panel = new RovRunReviewPanel(root, course);
                panel.Show(latest);
                Check("Review UI opens", !root.Q<VisualElement>("run-review").ClassListContains("review-closed"));
                panel.Dispose();
                Check("Review UI cleans up", root.Q<VisualElement>("run-review") == null);
            }
            finally { UnityEngine.Object.DestroyImmediate(fixture); }
            string afterHistory = File.Exists(historyPath) ? File.ReadAllText(historyPath) : null;
            Check("Synthetic validation did not write user history", beforeHistory == afterHistory);
            return string.Join("\n", checks);
        }

        // Explicit synthetic sample for visual QA; never appended to run history.
        public static RovRunReview Preview()
        {
            var recorder = new RovRunRecorder(); recorder.Reset(new Vector3(0, -1.2f, -2));
            var output = new float[] { 0.32f, 0.28f, 0.26f, 0.3f, 0.12f, 0.14f };
            for (int i = 0; i < 500; i++)
            {
                float z = -2 + i * 0.025f;
                recorder.Sample(new Vector3(Mathf.Sin(i * 0.02f) * 0.5f, -1.2f, z), Vector3.forward * 0.125f, 0.2f, output);
                if (i == 180 || i == 330) recorder.Impact(new Vector3(Mathf.Sin(i * 0.02f) * 0.5f, -1.2f, z));
            }
            var previous = new RovRunResult { seconds = 110, collisions = 3, depthRms = 0.23f, headingRms = 13, score = 580 };
            return recorder.Snapshot(new RovRunResult { scenarioName = "VISUAL VALIDATION SAMPLE", seconds = 100, collisions = 2, depthRms = 0.16f, headingRms = 9, score = 630 }, previous, true, true, true, 0);
        }
    }
}
