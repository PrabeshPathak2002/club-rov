using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ClubROV.Simulation.Editor
{
    public static class FpvFeedSetup
    {
        [MenuItem("Club ROV/Install FPV Camera Feed")]
        private static void InstallFromMenu() => Install();
        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Install outside Play Mode.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/PoolTraining.unity") throw new InvalidOperationException("Open PoolTraining first.");
            var rig = UnityEngine.Object.FindAnyObjectByType<RovCameraRig>();
            if (rig == null) throw new InvalidOperationException("Camera rig missing.");
            if (rig.GetComponent<RovFpvCameraFeed>() == null) Undo.AddComponent<RovFpvCameraFeed>(rig.gameObject);
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/ROV/Scripts/RovFpvFeed.shader");
            if (data == null || shader == null || !shader.isSupported) throw new InvalidOperationException("FPV shader or active renderer is unavailable.");
            RovFpvRendererFeature feature = null;
            foreach (var item in data.rendererFeatures) if (item is RovFpvRendererFeature existing) feature = existing;
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<RovFpvRendererFeature>();
                feature.name = "ROV FPV sensor feed";
                AssetDatabase.AddObjectToAsset(feature, data);
                data.rendererFeatures.Add(feature);
            }
            feature.Configure(shader); feature.SetActive(true);
            EditorUtility.SetDirty(feature); data.SetDirty(); EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            return "FPV 640x480/30 fps sensor feed installed; chase camera and UI remain native resolution.";
        }
    }
}
