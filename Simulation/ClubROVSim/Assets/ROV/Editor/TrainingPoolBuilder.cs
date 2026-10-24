using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ClubROV.Simulation.Editor
{
    public static class TrainingPoolBuilder
    {
        [MenuItem("Club ROV/Install Training Course")]
        private static void InstallFromMenu() => Install();
        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Install outside Play Mode.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/PoolTraining.unity") throw new InvalidOperationException("Open PoolTraining first.");
            if (GameObject.Find("Training course") != null) throw new InvalidOperationException("Training course already exists; preserve it.");
            var vehicle = UnityEngine.Object.FindAnyObjectByType<RovDynamics>();
            if (vehicle == null) throw new InvalidOperationException("Vehicle missing.");
            Resize("Pool floor", new(0, -3.65f, 0), new(20.4f, 0.3f, 24.4f));
            Resize("Port wall", new(-10.15f, -1.75f, 0), new(0.3f, 3.5f, 24.4f));
            Resize("Starboard wall", new(10.15f, -1.75f, 0), new(0.3f, 3.5f, 24.4f));
            Resize("Far wall", new(0, -1.75f, 12.15f), new(20, 3.5f, 0.3f));
            Resize("Near wall", new(0, -1.75f, -12.15f), new(20, 3.5f, 0.3f));
            foreach (var root in scene.GetRootGameObjects())
                if (root.name.StartsWith("Floor grid") || root.name == "Center lane" || root.name.StartsWith("Gate "))
                    Undo.DestroyObjectImmediate(root);
            var courseRoot = new GameObject("Training course");
            Undo.RegisterCreatedObjectUndo(courseRoot, "Install training course");
            Material yellow = AssetDatabase.LoadAssetAtPath<Material>("Assets/ROV/Generated/Gate_yellow.mat");
            Material grid = AssetDatabase.LoadAssetAtPath<Material>("Assets/ROV/Generated/Lane_marking.mat");
            Material cyan = NewMaterial("Course_cyan", new Color(0.15f, 0.95f, 0.93f));
            Material green = NewMaterial("Dock_green", new Color(0.3f, 0.92f, 0.38f));
            for (int x = -10; x <= 10; x++) Cube(courseRoot.transform, "Grid X", new(x, -3.493f, 0), new(0.015f, 0.005f, 24), grid, false);
            for (int z = -12; z <= 12; z++) Cube(courseRoot.transform, "Grid Z", new(0, -3.493f, z), new(20, 0.005f, 0.015f), grid, false);
            var gates = new Transform[3];
            Vector3[] positions = { new(0, -1.6f, 2.5f), new(3, -1.8f, 6), new(-2, -1.5f, 8.7f) };
            for (int i = 0; i < gates.Length; i++)
            {
                gates[i] = new GameObject($"Gate {i + 1}").transform;
                gates[i].SetParent(courseRoot.transform, false); gates[i].position = positions[i];
                Cube(gates[i], "Port post", new(-0.8f, 0, 0), new(0.07f, 1.5f, 0.07f), yellow);
                Cube(gates[i], "Starboard post", new(0.8f, 0, 0), new(0.07f, 1.5f, 0.07f), yellow);
                Cube(gates[i], "Top", new(0, 0.75f, 0), new(1.67f, 0.07f, 0.07f), yellow);
                Cube(gates[i], "Bottom", new(0, -0.75f, 0), new(1.67f, 0.07f, 0.07f), yellow);
                // Number of small pips identifies each gate without text/font dependencies.
                for (int pip = 0; pip <= i; pip++) Cube(gates[i], "Number pip", new(-0.15f * i + pip * 0.3f, 0.92f, 0), new(0.12f, 0.12f, 0.07f), cyan, false);
            }
            var hover = new GameObject("Cyan hover target — heading 000").transform;
            hover.SetParent(courseRoot.transform, false); hover.position = new(-2, -1.5f, 10.5f);
            Ring(hover, cyan, false); Ring(hover, cyan, true);
            var dock = new GameObject("Green dock target — heading 180").transform;
            dock.SetParent(courseRoot.transform, false); dock.SetPositionAndRotation(new(3, -2.94f, 9.5f), Quaternion.Euler(0, 180, 0));
            Cube(dock, "Dock platform", new(0, -0.21f, 0), new(1.4f, 0.08f, 1.2f), green);
            Cube(dock, "Port guide", new(-0.65f, -0.05f, 0), new(0.06f, 0.28f, 1.2f), green);
            Cube(dock, "Starboard guide", new(0.65f, -0.05f, 0), new(0.06f, 0.28f, 1.2f), green);
            Cube(dock, "Rear stop", new(0, -0.05f, -0.57f), new(1.3f, 0.28f, 0.06f), green);
            var course = Undo.AddComponent<RovTrainingCourse>(vehicle.gameObject);
            course.Configure(gates, hover, dock);
            var rig = UnityEngine.Object.FindAnyObjectByType<RovCameraRig>();
            var water = Undo.AddComponent<RovWaterVisibility>(rig.gameObject);
            var hud = UnityEngine.Object.FindAnyObjectByType<RovHud>();
            Undo.RecordObject(hud, "Connect training HUD");
            hud.ConfigureTraining(course, water);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            return "20 x 24 m pool, three gates, hover target, dock and visibility controls installed.";
        }
        private static void Resize(string name, Vector3 position, Vector3 size)
        {
            var item = GameObject.Find(name);
            if (item == null) throw new InvalidOperationException("Missing pool object: " + name);
            Undo.RecordObject(item.transform, "Expand pool");
            item.transform.position = position; item.transform.localScale = size;
        }
        private static Material NewMaterial(string name, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            AssetDatabase.CreateAsset(material, "Assets/ROV/Generated/" + name + ".mat");
            return material;
        }
        private static void Cube(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool solid = true)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name; item.transform.SetParent(parent, false); item.transform.localPosition = position; item.transform.localScale = size;
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
        }
        private static void Ring(Transform parent, Material material, bool vertical)
        {
            var line = new GameObject("Hover ring").AddComponent<LineRenderer>();
            line.transform.SetParent(parent, false); line.useWorldSpace = false; line.loop = true;
            line.positionCount = 64; line.startWidth = line.endWidth = 0.025f; line.sharedMaterial = material;
            for (int i = 0; i < 64; i++)
            {
                float a = i * 2 * Mathf.PI / 64;
                line.SetPosition(i, vertical ? new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 0.45f : new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.45f);
            }
        }
    }
}
