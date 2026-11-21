using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ClubROV.Simulation.Editor
{
    public static class RovSensorLearningSetup
    {
        public const string PlayerPath = "Builds/SensorTraining/ClubROVSim.exe";
        [MenuItem("ROV/Reinforcement Learning/Build sensor-only training player")]
        public static void BuildTrainingPlayer()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play before the sensor player build.");
            const string source = "Assets/ROV/Models/PoolCourse-generalise-20261107.onnx";
            const string target = "Assets/ROV/Resources/SensorCourseTeacher.onnx";
            if (!AssetDatabase.IsValidFolder("Assets/ROV/Resources"))
                AssetDatabase.CreateFolder("Assets/ROV", "Resources");
            if (AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>(target) == null &&
                !AssetDatabase.CopyAsset(source, target))
                throw new InvalidOperationException("Cannot preserve the teacher model for demonstration collection.");
            AssetDatabase.SaveAssets();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/PoolTraining.unity" },
                locationPathName = PlayerPath, target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Sensor training player build {report.summary.result}.");
            Debug.Log($"Sensor-only training player built at {PlayerPath}.");
        }
    }
}
