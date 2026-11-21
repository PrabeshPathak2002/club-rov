using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ClubROV.Simulation.Editor
{
    public static class RovDvlSetup
    {
        [MenuItem("ROV/ROS2/Install navigation sensor stream")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode before installing.");
            var vehicle = UnityEngine.Object.FindAnyObjectByType<RovDynamics>();
            if (vehicle == null) throw new InvalidOperationException("Open the training scene first.");
            if (vehicle.GetComponent<RovDvlSensor>() == null) Undo.AddComponent<RovDvlSensor>(vehicle.gameObject);
            RovDvlVisuals.InstallFor(vehicle);
            EditorSceneManager.MarkSceneDirty(vehicle.gameObject.scene);
            Selection.activeGameObject = vehicle.gameObject;
        }
        [MenuItem("ROV/ROS2/Start sensor stream (Play Mode)")]
        public static void StartStream()
        {
            var sensor = UnityEngine.Object.FindAnyObjectByType<RovDvlSensor>();
            if (sensor == null) throw new InvalidOperationException("Install the navigation sensor component first.");
            sensor.StartStreaming();
        }
        [MenuItem("ROV/ROS2/Stop sensor stream")]
        public static void StopStream() => UnityEngine.Object.FindAnyObjectByType<RovDvlSensor>()?.StopStreaming();
    }
}
