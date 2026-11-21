using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Unity.InferenceEngine;

namespace ClubROV.Simulation.Editor
{
    [InitializeOnLoad]
    public static class RovLearningSetup
    {
        private const string EnabledKey = "ClubROV.RL.Enabled";
        private const string PolicyKey = "ClubROV.RL.Policy";
        private const string TrainingPlayerPath = "Builds/Training/ClubROVSim.exe";
        static RovLearningSetup() => EditorApplication.playModeStateChanged += PlayModeChanged;

        [MenuItem("ROV/Reinforcement Learning/Enable first-course training")]
        public static void Enable()
        {
            if (EditorApplication.isPlaying)
                throw new System.InvalidOperationException("Stop Play before enabling training.");
            SessionState.SetBool(EnabledKey, true);
            SessionState.SetString(PolicyKey, "");
            Debug.Log("RL enabled for this Editor session. Start the PPO trainer, then enter Play Mode.");
        }

        [MenuItem("ROV/Reinforcement Learning/Return to manual piloting")]
        public static void Disable()
        {
            SessionState.SetBool(EnabledKey, false);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            Debug.Log("RL disabled. The next Play session uses normal piloting.");
        }

        [MenuItem("ROV/Reinforcement Learning/Run selected trained policy")]
        public static void EvaluatePolicy()
        {
            if (EditorApplication.isPlaying || Selection.activeObject is not ModelAsset)
                throw new System.InvalidOperationException("Stop Play and select an imported PoolCourse ONNX model first.");
            SessionState.SetString(PolicyKey, AssetDatabase.GetAssetPath(Selection.activeObject));
            SessionState.SetBool(EnabledKey, true);
            Debug.Log("Policy selected. Enter Play Mode to evaluate it on the first course.");
        }

        [MenuItem("ROV/Reinforcement Learning/Build parallel training player")]
        public static void BuildTrainingPlayer()
        {
            if (EditorApplication.isPlaying)
                throw new System.InvalidOperationException("Stop Play before building the training player.");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/PoolTraining.unity" },
                locationPathName = TrainingPlayerPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.InvalidOperationException($"Training player build {report.summary.result}.");
            Debug.Log($"Training player built at {TrainingPlayerPath}. Start it with Start-Parallel-Training.bat.");
        }

        private static void PlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode && SessionState.GetBool(EnabledKey, false))
                Time.timeScale = 1;
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(EnabledKey, false))
                StartTraining();
        }

        public static void StartTraining()
        {
            ModelAsset model = null;
            string modelPath = SessionState.GetString(PolicyKey, "");
            if (!string.IsNullOrEmpty(modelPath))
            {
                model = AssetDatabase.LoadAssetAtPath<ModelAsset>(modelPath);
                if (model == null) throw new System.InvalidOperationException("Selected RL model is missing.");
            }
            RovLearningEnvironment.Start(model);
        }
    }
}
