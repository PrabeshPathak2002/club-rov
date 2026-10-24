using System;
using System.IO;
using ClubROV.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace ClubROV.Simulation.Editor
{
    public static class PoolSceneBuilder
    {
        private const string Root = "Assets/ROV";
        private const string ScenePath = "Assets/Scenes/PoolTraining.unity";

        [MenuItem("Club ROV/Create Pool Training Scene")]
        public static void Create()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("PoolTraining already exists; preserve it and open that scene.");
            Directory.CreateDirectory(Root + "/Generated");
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) throw new InvalidOperationException("The pool requires the Universal render pipeline.");
            urp.supportsHDR = true;
            if (QualitySettings.names.Length > 0) QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
            Physics.gravity = new Vector3(0, -9.81f, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.38f, 0.57f, 0.65f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.035f;
            RenderSettings.fogColor = new Color(0.06f, 0.26f, 0.32f);

            Material tile = Material("Pool tile", new Color(0.53f, 0.77f, 0.79f));
            Material wall = Material("Pool wall", new Color(0.78f, 0.88f, 0.88f));
            Material line = Material("Lane marking", new Color(0.025f, 0.13f, 0.21f));
            Material yellow = Material("ROV yellow", new Color(1, 0.72f, 0.08f));
            Material dark = Material("ROV graphite", new Color(0.09f, 0.13f, 0.15f));
            Material target = Material("Gate yellow", new Color(1, 0.62f, 0.06f));

            Cube("Pool floor", new Vector3(0, -3.65f, 0), new Vector3(10.4f, 0.3f, 12.4f), tile);
            Cube("Port wall", new Vector3(-5.15f, -1.75f, 0), new Vector3(0.3f, 3.5f, 12.4f), wall);
            Cube("Starboard wall", new Vector3(5.15f, -1.75f, 0), new Vector3(0.3f, 3.5f, 12.4f), wall);
            Cube("Far wall", new Vector3(0, -1.75f, 6.15f), new Vector3(10, 3.5f, 0.3f), wall);
            Cube("Near wall", new Vector3(0, -1.75f, -6.15f), new Vector3(10, 3.5f, 0.3f), wall);
            for (int x = -5; x <= 5; x++) Cube("Floor grid X", new Vector3(x, -3.493f, 0), new Vector3(0.012f, 0.005f, 12), line, false);
            for (int z = -6; z <= 6; z++) Cube("Floor grid Z", new Vector3(0, -3.493f, z), new Vector3(10, 0.005f, 0.012f), line, false);
            Cube("Center lane", new Vector3(0, -3.48f, 0), new Vector3(0.12f, 0.01f, 10), line, false);
            Cube("Gate port post", new Vector3(-0.8f, -1.55f, 2.5f), new Vector3(0.07f, 1.5f, 0.07f), target);
            Cube("Gate starboard post", new Vector3(0.8f, -1.55f, 2.5f), new Vector3(0.07f, 1.5f, 0.07f), target);
            Cube("Gate top", new Vector3(0, -0.8f, 2.5f), new Vector3(1.67f, 0.07f, 0.07f), target);

            var vehicle = new GameObject("Club ROV - simulation");
            vehicle.transform.position = new Vector3(0, -1.2f, -1);
            Mesh mesh = ImportAssembly();
            var visual = new GameObject("September assembly visual");
            visual.transform.SetParent(vehicle.transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            visual.AddComponent<MeshRenderer>().sharedMaterial = yellow;
            var collider = vehicle.AddComponent<BoxCollider>();
            collider.size = RovThrusterLayout.EnvelopeMetres;
            var rb = vehicle.AddComponent<Rigidbody>();
            rb.mass = 8;
            rb.linearDamping = 0;
            rb.angularDamping = 0;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.maxAngularVelocity = 1.2f;
            var dynamics = vehicle.AddComponent<RovDynamics>();
            vehicle.AddComponent<RovPilotInput>();
            for (int i = 0; i < RovThrusterLayout.Count; i++)
            {
                var mount = new GameObject(i < 4 ? $"Horizontal force {i + 1}" : $"Vertical force {i - 3}");
                mount.transform.SetParent(vehicle.transform, false);
                mount.transform.localPosition = RovThrusterLayout.Position(i);
                mount.transform.localRotation = Quaternion.LookRotation(RovThrusterLayout.Direction(i));
            }

            var lamp = new GameObject("Pool daylight").AddComponent<Light>();
            lamp.type = LightType.Directional;
            lamp.intensity = 2;
            lamp.color = new Color(0.85f, 0.95f, 1);
            lamp.shadows = LightShadows.Soft;
            lamp.transform.rotation = Quaternion.Euler(40, -25, 0);
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.nearClipPlane = 0.025f;
            camera.farClipPlane = 50;
            camera.fieldOfView = 65;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
            camera.gameObject.AddComponent<AudioListener>();
            camera.transform.position = vehicle.transform.position + new Vector3(1.1f, 0.65f, -1.4f);
            camera.transform.LookAt(vehicle.transform);
            camera.gameObject.AddComponent<RovCameraRig>().Configure(vehicle.transform);
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.volumeLayerMask = 1;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<Tonemapping>(true).mode.value = TonemappingMode.ACES;
            var bloom = profile.Add<Bloom>(true); bloom.intensity.value = 0.5f; bloom.threshold.value = 0.9f;
            profile.Add<Vignette>(true).intensity.value = 0.18f;
            AssetDatabase.CreateAsset(profile, Root + "/Generated/PoolVolume.asset");
            var volume = new GameObject("Underwater grade").AddComponent<Volume>();
            volume.isGlobal = true; volume.sharedProfile = profile;

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1600, 900);
            AssetDatabase.CreateAsset(panel, Root + "/Generated/HudPanel.asset");
            var hudObject = new GameObject("Pilot HUD");
            var document = hudObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root + "/UI/RovHud.uxml");
            if (document.visualTreeAsset == null) throw new InvalidOperationException("HUD UXML was not imported.");
            hudObject.AddComponent<RovHud>().Configure(dynamics);
            PlayerSettings.runInBackground = true;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = vehicle;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log("ROV pool created. Provisional physics; simulation only. " + ValidateMixer());
        }

        private static Material Material(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader missing.");
            var material = new Material(shader) { name = name, color = color };
            material.SetFloat("_Smoothness", 0.28f);
            AssetDatabase.CreateAsset(material, Root + "/Generated/" + name.Replace(' ', '_') + ".mat");
            return material;
        }

        private static void Cube(string name, Vector3 position, Vector3 size, Material material, bool collision = true)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name; item.transform.position = position; item.transform.localScale = size;
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
        }

        private static Mesh ImportAssembly()
        {
            string source = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Models/club-rov-preview.stl"));
            using var file = new BinaryReader(File.OpenRead(source));
            file.ReadBytes(80);
            uint faces = file.ReadUInt32();
            if (faces == 0 || faces > 1000000 || file.BaseStream.Length != 84L + faces * 50L)
                throw new InvalidDataException("Expected a bounded binary assembly STL.");
            var vertices = new Vector3[faces * 3];
            var triangles = new int[vertices.Length];
            for (int face = 0; face < faces; face++)
            {
                file.ReadBytes(12);
                for (int corner = 0; corner < 3; corner++)
                {
                    float x = file.ReadSingle(), y = file.ReadSingle(), z = file.ReadSingle();
                    vertices[face * 3 + corner] = new Vector3(-y, z, x) * 0.001f;
                }
                // Reflect CAD coordinates into Unity's convention, retaining outward faces.
                triangles[face * 3] = face * 3;
                triangles[face * 3 + 1] = face * 3 + 2;
                triangles[face * 3 + 2] = face * 3 + 1;
                file.ReadUInt16();
            }
            var mesh = new Mesh { name = "Club ROV assembly", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateBounds();
            Vector3 center = mesh.bounds.center;
            for (int i = 0; i < vertices.Length; i++) vertices[i] -= center;
            mesh.vertices = vertices; mesh.RecalculateBounds(); mesh.RecalculateNormals();
            AssetDatabase.CreateAsset(mesh, Root + "/Generated/ClubROV.asset");
            return mesh;
        }

        [MenuItem("Club ROV/Apply Measured Geometry")]
        private static void ApplyMeasuredGeometryFromMenu() => ApplyMeasuredGeometry();

        public static string ApplyMeasuredGeometry()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Apply geometry outside Play Mode.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open the saved PoolTraining scene first.");
            var vehicle = UnityEngine.Object.FindAnyObjectByType<RovDynamics>();
            if (vehicle == null) throw new InvalidOperationException("Vehicle is missing.");
            var collider = vehicle.GetComponent<BoxCollider>();
            Undo.RecordObject(collider, "Apply Fusion envelope");
            collider.size = RovThrusterLayout.EnvelopeMetres;
            for (int i = 0; i < RovThrusterLayout.Count; i++)
            {
                string name = i < 4 ? $"Horizontal force {i + 1}" : $"Vertical force {i - 3}";
                Transform mount = vehicle.transform.Find(name);
                if (mount == null) throw new InvalidOperationException("Missing force marker: " + name);
                Undo.RecordObject(mount, "Apply Fusion thrust axis");
                mount.localPosition = RovThrusterLayout.Position(i);
                mount.localRotation = Quaternion.LookRotation(RovThrusterLayout.Direction(i));
            }
            var serialized = new SerializedObject(vehicle);
            serialized.FindProperty("hullHeight").floatValue = RovThrusterLayout.EnvelopeMetres.y;
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(vehicle.gameObject.scene);
            EditorSceneManager.SaveScene(vehicle.gameObject.scene);
            return "Measured envelope and all six force markers saved. " + ValidateMixer();
        }

        public static string ValidateMixer()
        {
            int checks = 0;
            var outputs = new float[6];
            foreach (var command in new[] { Vector3.zero, Vector3.forward, Vector3.right, Vector3.up })
            {
                RovThrusterLayout.Mix(command, 0, outputs);
                Vector3 force = Vector3.zero, torque = Vector3.zero;
                for (int i = 0; i < 6; i++)
                {
                    Vector3 f = RovThrusterLayout.Direction(i) * outputs[i];
                    force += f; torque += Vector3.Cross(RovThrusterLayout.Position(i), f);
                }
                if (command == Vector3.zero ? force.magnitude > 0.001f : Vector3.Dot(force, command) <= 0)
                    throw new InvalidOperationException("Mixer force check failed.");
                if (Mathf.Abs(torque.y) > 0.001f) throw new InvalidOperationException("Translation introduced yaw.");
                checks++;
            }
            RovThrusterLayout.Mix(Vector3.zero, 1, outputs);
            Vector3 net = Vector3.zero, turn = Vector3.zero;
            for (int i = 0; i < 6; i++)
            {
                Vector3 force = RovThrusterLayout.Direction(i) * outputs[i];
                net += force; turn += Vector3.Cross(RovThrusterLayout.Position(i), force);
            }
            if (net.magnitude > 0.001f || turn.y <= 0) throw new InvalidOperationException("Yaw mixing failed.");
            checks++;
            RovThrusterLayout.Mix(Vector3.one, 1, outputs);
            foreach (float value in outputs) if (Mathf.Abs(value) > 1.0001f) throw new InvalidOperationException("Command saturation failed.");
            return $"{checks + 1} mixer checks passed.";
        }
    }
}
