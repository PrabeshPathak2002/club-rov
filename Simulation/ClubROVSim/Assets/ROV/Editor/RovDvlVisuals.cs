using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClubROV.Simulation.Editor
{
    public static class RovDvlVisuals
    {
        private const string Source = "Assets/ROV/Art/Tracker650/Tracker650Exterior.json";
        private const string Generated = "Assets/ROV/Generated/Tracker650";
        public const string RootName = "Tracker 650 DVL visual";
        [Serializable] private sealed class SourceData { public float[] reference_mm; public Part[] parts; }
        [Serializable] private sealed class Part { public string name, material; public float[] vertices; public int[] triangles; }

        [MenuItem("ROV/ROS2/Install DVL visual")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Install the visual outside Play Mode.");
            var vehicle = UnityEngine.Object.FindAnyObjectByType<RovDynamics>();
            if (vehicle == null) throw new InvalidOperationException("Open the training scene first.");
            InstallFor(vehicle);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(vehicle.gameObject.scene);
            Selection.activeGameObject = vehicle.gameObject;
        }

        public static void InstallFor(RovDynamics vehicle)
        {
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(Source);
            if (source == null) throw new InvalidOperationException("Import the Tracker650 CAD JSON asset first.");
            var data = JsonUtility.FromJson<SourceData>(source.text);
            if (data?.reference_mm?.Length != 3 || data.parts == null || data.parts.Length == 0)
                throw new InvalidOperationException("Invalid DVL mesh source.");
            if (!AssetDatabase.IsValidFolder(Generated)) AssetDatabase.CreateFolder("Assets/ROV/Generated", "Tracker650");
            var old = vehicle.transform.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Install Tracker 650 visual");
            root.transform.SetParent(vehicle.transform, false);
            foreach (string group in new[] { "housing", "face", "metal", "mount" })
            {
                var vertices = new List<Vector3>();
                var indices = new List<int>();
                foreach (var part in data.parts)
                {
                    if (part.material != group) continue;
                    if (part.vertices.Length % 3 != 0 || part.triangles.Length % 3 != 0)
                        throw new InvalidOperationException("Invalid triangulation: " + part.name);
                    int offset = vertices.Count;
                    for (int i = 0; i < part.vertices.Length; i += 3)
                    {
                        float x = part.vertices[i] - data.reference_mm[0];
                        float y = part.vertices[i+1] - data.reference_mm[1];
                        float z = part.vertices[i+2] - data.reference_mm[2];
                        vertices.Add(new Vector3(-y,z,x)*0.001f);
                    }
                    for (int i = 0; i < part.triangles.Length; i += 3)
                    {
                        indices.Add(offset+part.triangles[i]);
                        // CAD -> Unity is a reflection; preserve outward-facing triangles.
                        indices.Add(offset+part.triangles[i+2]);
                        indices.Add(offset+part.triangles[i+1]);
                    }
                }
                if (vertices.Count == 0) throw new InvalidOperationException("Missing DVL mesh group: " + group);
                string meshPath = Generated + "/" + group + ".asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (mesh == null)
                {
                    mesh = new Mesh { name = "Tracker 650 " + group };
                    AssetDatabase.CreateAsset(mesh,meshPath);
                }
                mesh.Clear(); mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(vertices); mesh.SetTriangles(indices,0);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh);
                string matPath = Generated + "/" + group + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Tracker 650 " + group };
                    material.color = group == "metal" ? new Color(0.58f,0.64f,0.67f) :
                        group == "face" ? new Color(0.025f,0.045f,0.05f) :
                        group == "mount" ? new Color(0.13f,0.19f,0.21f) : new Color(0.25f,0.3f,0.32f);
                    material.SetFloat("_Smoothness",group == "metal" ? 0.6f : 0.3f);
                    material.SetFloat("_Metallic",group == "metal" ? 0.8f : 0);
                    AssetDatabase.CreateAsset(material,matPath);
                }
                var partObject = new GameObject("DVL " + group);
                partObject.transform.SetParent(root.transform,false);
                partObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                partObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            AssetDatabase.SaveAssets();
        }

        public static string Validate()
        {
            var vehicle = UnityEngine.Object.FindAnyObjectByType<RovDynamics>();
            var root = vehicle.transform.Find(RootName);
            if (root == null || root.childCount != 4 || !root.gameObject.activeInHierarchy)
                throw new InvalidOperationException("DVL visual is not installed and active.");
            if (root.GetComponentsInChildren<Collider>().Length != 0 || root.GetComponentsInChildren<Rigidbody>().Length != 0)
                throw new InvalidOperationException("The visual must not alter the provisional physics model.");
            var housing = root.Find("DVL housing").GetComponent<MeshFilter>().sharedMesh.bounds;
            if (Mathf.Abs(housing.size.x-0.085f)>0.0005f || Mathf.Abs(housing.size.z-0.085f)>0.0005f ||
                Mathf.Abs(housing.min.y+0.1926006f)>0.0005f || Mathf.Abs(housing.max.y+0.135f)>0.0005f)
                throw new InvalidOperationException("DVL housing scale or mounting position differs from CAD.");
            var mount = root.Find("DVL mount").GetComponent<MeshFilter>().sharedMesh.bounds;
            if (Mathf.Abs(mount.size.x-0.18f)>0.0005f || Mathf.Abs(mount.size.z-0.13f)>0.0005f ||
                Mathf.Abs(mount.size.y-0.005f)>0.0001f)
                throw new InvalidOperationException("Adapter scale differs from CAD.");
            int triangles = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0 || !filter.GetComponent<Renderer>().enabled)
                    throw new InvalidOperationException("A DVL renderer is missing or disabled.");
                triangles += filter.sharedMesh.triangles.Length/3;
            }
            return "DVL visual active: 4 renderers, " + triangles + " triangles; CAD scale/position and physics isolation passed.";
        }
    }
}
