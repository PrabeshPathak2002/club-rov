using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ClubROV.Simulation.Editor
{
    public static class TrainingUpgradeSetup
    {
        public static string Install()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/PoolTraining.unity") throw new InvalidOperationException("Open PoolTraining.");
            var vehicle=UnityEngine.Object.FindAnyObjectByType<RovDynamics>();
            var water=UnityEngine.Object.FindAnyObjectByType<RovWaterVisibility>();
            var hud=UnityEngine.Object.FindAnyObjectByType<RovHud>();
            var optics=water.GetComponent<RovUnderwaterCamera>() ?? Undo.AddComponent<RovUnderwaterCamera>(water.gameObject);
            optics.ConfigureShader(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            var settings=vehicle.GetComponent<RovTrainingSettings>() ?? Undo.AddComponent<RovTrainingSettings>(vehicle.gameObject);
            settings.Configure(water,water.GetComponent<RovFpvCameraFeed>(),optics);
            var panel=hud.GetComponent<RovTrainingPanel>() ?? Undo.AddComponent<RovTrainingPanel>(hud.gameObject);
            panel.Configure(settings);
            EditorUtility.SetDirty(optics); EditorUtility.SetDirty(settings); EditorUtility.SetDirty(panel);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            return "Training upgrades installed: arming badge, scenarios, disturbances, scores and underwater FPV.";
        }
    }
}
