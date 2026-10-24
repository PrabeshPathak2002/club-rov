using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ClubROV.Simulation.Editor
{
    public static class SitlSetup
    {
        public static string Install(string simulatorAddress = "127.0.0.1")
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/PoolTraining.unity") throw new InvalidOperationException("Open PoolTraining.");
            var vehicle = UnityEngine.Object.FindAnyObjectByType<RovDynamics>();
            if (vehicle == null) throw new InvalidOperationException("Vehicle missing.");
            var bridge = vehicle.GetComponent<RovSitlBridge>();
            if (bridge == null) bridge = Undo.AddComponent<RovSitlBridge>(vehicle.gameObject);
            Undo.RecordObject(bridge, "Configure SITL endpoint"); bridge.Configure(simulatorAddress);
            EditorUtility.SetDirty(bridge); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            return "SITL bridge installed, disabled until CONNECT SITL is pressed. Peer: " + bridge.SimulatorAddress;
        }
    }
}
