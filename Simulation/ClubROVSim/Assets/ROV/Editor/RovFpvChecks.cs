using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClubROV.Simulation.Editor
{
    public static class RovFpvChecks
    {
        [Serializable] private sealed class Result { public string name; public bool passed; public string observation; }
        [Serializable] private sealed class Report { public bool complete; public List<Result> checks = new(); }
        private static Report report;
        private static RovCameraRig rig;
        private static RovFpvCameraFeed feed;
        private static RovPilotInput pilot;
        private static Camera camera;
        private static RovCameraRig.ViewMode originalView;
        private static bool originalEnabled;
        private static float started, chaseFov, chaseAspect;
        private static int stage, captures, frames;
        public static string Start()
        {
            if (!EditorApplication.isPlaying || rig != null) throw new InvalidOperationException("Start once in Play Mode.");
            rig = UnityEngine.Object.FindAnyObjectByType<RovCameraRig>();
            feed = rig.GetComponent<RovFpvCameraFeed>(); camera = rig.GetComponent<Camera>();
            pilot = UnityEngine.Object.FindAnyObjectByType<RovPilotInput>();
            originalView = rig.Mode; originalEnabled = pilot.enabled; pilot.enabled = false;
            pilot.GetComponent<RovDynamics>().ResetVehicle();
            rig.SetView(RovCameraRig.ViewMode.Chase);
            rig.SendMessage("LateUpdate"); chaseFov = camera.fieldOfView; chaseAspect = camera.aspect;
            report = new Report(); stage = 0; started = Time.unscaledTime;
            EditorApplication.update += Tick;
            return "FPV rendering, cadence and resource checks started.";
        }
        private static void Check(string name, bool pass, string observation = "") => report.checks.Add(new Result { name = name, passed = pass, observation = observation });
        private static void Tick()
        {
            if (!EditorApplication.isPlaying || rig == null) { Finish(false); return; }
            float elapsed = Time.unscaledTime - started;
            if (elapsed < (stage == 1 ? 1 : 0.3f)) return;
            switch (stage)
            {
                case 0:
                    Check("Chase bypasses effect and releases buffer", !feed.Active && !feed.BufferAllocated);
                    rig.ToggleView(); captures = feed.CapturedFrames; frames = Time.frameCount;
                    break;
                case 1:
                    int count = feed.CapturedFrames - captures;
                    Check("FPV allocates a low-resolution history", feed.Active && feed.BufferAllocated && feed.Width == 640 && feed.Height == 480);
                    Check("Video cadence is capped independently of simulation", count > 0 && count <= Mathf.CeilToInt(elapsed * feed.FramesPerSecond) + 2,
                        $"{count} samples over {elapsed:F3}s, {Time.frameCount - frames} simulation frames");
                    Check("Pi-camera projection and aspect", Mathf.Abs(camera.fieldOfView - 48.8f) < 0.01f && Mathf.Abs(camera.aspect - 4f / 3) < 0.001f);
                    Check("HUD remains a native-resolution overlay", UnityEngine.Object.FindAnyObjectByType<RovHud>().GetComponent<UIDocument>().panelSettings.targetTexture == null);
                    Check("Shader compiles on the active renderer", !ShaderUtil.ShaderHasError(AssetDatabase.LoadAssetAtPath<Shader>("Assets/ROV/Scripts/RovFpvFeed.shader")));
                    rig.ToggleView();
                    break;
                case 2:
                    Check("Chase restores lens and viewport", Mathf.Abs(camera.fieldOfView - chaseFov) < 0.01f && Mathf.Abs(camera.aspect - chaseAspect) < 0.001f);
                    Check("Switching to chase frees video history", !feed.Active && !feed.BufferAllocated);
                    rig.ToggleView();
                    break;
                case 3:
                    Check("Returning to FPV recreates video history", feed.Active && feed.BufferAllocated);
                    feed.enabled = false;
                    break;
                case 4:
                    Check("Disabling effect releases resources", !feed.Active && !feed.BufferAllocated);
                    Finish(true); return;
            }
            stage++; started = Time.unscaledTime;
        }
        private static void Finish(bool complete)
        {
            EditorApplication.update -= Tick;
            if (report != null)
            {
                report.complete = complete;
                File.WriteAllText(Path.Combine(Application.dataPath, "../FpvCheckResults.json"), JsonUtility.ToJson(report, true));
            }
            if (feed != null) feed.enabled = true;
            if (rig != null) rig.SetView(originalView);
            if (pilot != null) { pilot.GetComponent<RovDynamics>().ResetVehicle(); pilot.enabled = originalEnabled; }
            rig = null; feed = null; pilot = null; camera = null;
        }
    }
}
