using System.Collections.Generic;
using UnityEngine;

namespace ClubROV.Simulation
{
    public sealed class RovAppearance : MonoBehaviour
    {
        private MeshFilter visual;
        private Mesh original, colored;
        private Material[] originalMaterials, palette;
        private RovDynamics vehicle;
        private readonly Transform[] rotors = new Transform[RovThrusterLayout.Count];
        private readonly Mesh[] rotorMeshes = new Mesh[RovThrusterLayout.Count];
        private readonly float[] angles = new float[RovThrusterLayout.Count];
        private readonly Mesh[] blurMeshes = new Mesh[RovThrusterLayout.Count];
        private readonly MeshRenderer[] blurRenderers = new MeshRenderer[RovThrusterLayout.Count];
        private readonly Vector3[] rotorAxes = new Vector3[RovThrusterLayout.Count];
        private Material blurMaterial;
        // Visual estimate: manufacturer U01 data does not publish verified loaded RPM.
        public const float VisualFullOutputRpm = 3000;
        public static float RotorDegreesPerSecond(float output) => Mathf.Clamp(output, -1, 1) * VisualFullOutputRpm * 6;
        public static Vector3 RotorPivot(int index, Vector3 approximateCenter)
        {
            Vector3 shaft = RovThrusterLayout.Position(index), axis = RovThrusterLayout.Direction(index);
            return shaft + axis * Vector3.Dot(approximateCenter - shaft, axis);
        }

        private void Awake()
        {
            foreach (var candidate in GetComponentsInChildren<MeshFilter>())
                if (candidate.name == "September assembly visual") { visual = candidate; break; }
            if (visual == null || visual.sharedMesh == null) return;
            var renderer = visual.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.sharedMaterial == null) return;
            original = visual.sharedMesh;
            originalMaterials = renderer.sharedMaterials;
            colored = Instantiate(original);
            colored.name = "Club ROV multicolor visual";
            var assignments = Resources.Load<TextAsset>("RovFusionColors");
            if (assignments == null || assignments.bytes.Length != original.triangles.Length / 3)
            {
                Destroy(colored);
                colored = null;
                Debug.LogWarning("Fusion color map is missing or does not match the assembly mesh.");
                return;
            }
            var colorsByTriangle = assignments.bytes;
            var regions = new List<int>[5];
            for (int i = 0; i < regions.Length; i++) regions[i] = new List<int>();
            var triangles = original.triangles;
            var rotorTriangles = new List<int>[RovThrusterLayout.Count];
            for (int i = 0; i < rotorTriangles.Length; i++) rotorTriangles[i] = new List<int>();
            var vertices = original.vertices;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int region = colorsByTriangle[i / 3];
                if (region == 3)
                {
                    Vector3 center = transform.InverseTransformPoint(visual.transform.TransformPoint(
                        (vertices[triangles[i]] + vertices[triangles[i + 1]] + vertices[triangles[i + 2]]) / 3));
                    int nearest = 0; float distance = float.PositiveInfinity;
                    for (int n = 0; n < rotorTriangles.Length; n++)
                    {
                        float candidate = (center - RovThrusterLayout.Position(n)).sqrMagnitude;
                        if (candidate < distance) { nearest = n; distance = candidate; }
                    }
                    rotorTriangles[nearest].Add(triangles[i]); rotorTriangles[nearest].Add(triangles[i + 1]);
                    rotorTriangles[nearest].Add(triangles[i + 2]);
                    continue;
                }
                regions[region].Add(triangles[i]);
                regions[region].Add(triangles[i + 1]);
                regions[region].Add(triangles[i + 2]);
            }
            colored.subMeshCount = regions.Length;
            for (int i = 0; i < regions.Length; i++) colored.SetTriangles(regions[i], i);
            Color[] colors = { new(0.95f, 0.69f, 0.12f), new(0.12f, 0.14f, 0.15f),
                new(0.08f, 0.44f, 0.16f), new(0.78f, 0.07f, 0.09f), new(0.65f, 0.71f, 0.73f) };
            string[] names = { "Fusion yellow deck and handle", "Fusion graphite frame",
                "Fusion green thruster housings", "Fusion red rotors", "Fusion silver enclosure" };
            palette = new Material[colors.Length];
            for (int i = 0; i < palette.Length; i++)
            {
                palette[i] = new Material(originalMaterials[0]) { name = names[i], color = colors[i] };
                palette[i].SetFloat("_Smoothness", 0.35f);
            }
            visual.sharedMesh = colored;
            renderer.sharedMaterials = palette;
            blurMaterial = new Material(palette[3]) { name = "Rotor high-speed blur", renderQueue = 3000 };
            blurMaterial.SetFloat("_Surface", 1); blurMaterial.SetFloat("_ZWrite", 0);
            blurMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            blurMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            blurMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            blurMaterial.color = new Color(colors[3].r, colors[3].g, colors[3].b, 0.07f);
            var normals = original.normals;
            for (int n = 0; n < rotors.Length; n++)
            {
                var indices = rotorTriangles[n];
                if (indices.Count == 0) continue;
                var bounds = new Bounds(vertices[indices[0]], Vector3.zero);
                foreach (int index in indices) bounds.Encapsulate(vertices[index]);
                Vector3 centerInVehicle = transform.InverseTransformPoint(visual.transform.TransformPoint(bounds.center));
                Vector3 pivot = visual.transform.InverseTransformPoint(transform.TransformPoint(RotorPivot(n, centerInVehicle)));
                rotorAxes[n] = visual.transform.InverseTransformDirection(transform.TransformDirection(RovThrusterLayout.Direction(n))).normalized;
                var positions = new Vector3[indices.Count]; var rotorNormals = new Vector3[indices.Count];
                var faces = new int[indices.Count];
                for (int j = 0; j < indices.Count; j++)
                { positions[j] = vertices[indices[j]] - pivot; rotorNormals[j] = normals[indices[j]]; faces[j] = j; }
                var mesh = new Mesh { name = "Animated thruster rotor " + (n + 1) };
                mesh.vertices = positions; mesh.normals = rotorNormals; mesh.triangles = faces; mesh.RecalculateBounds();
                rotorMeshes[n] = mesh;
                var rotor = new GameObject("Thruster rotor " + (n + 1));
                rotor.layer = visual.gameObject.layer; rotor.transform.SetParent(visual.transform, false);
                rotor.transform.localPosition = pivot; rotors[n] = rotor.transform;
                rotor.AddComponent<MeshFilter>().sharedMesh = mesh;
                rotor.AddComponent<MeshRenderer>().sharedMaterial = palette[3];
                const int samples = 8;
                var blurPositions = new List<Vector3>(); var blurNormals = new List<Vector3>(); var blurFaces = new List<int>();
                for (int sample = 0; sample < samples; sample++)
                {
                    Quaternion rotation = Quaternion.AngleAxis(360f * sample / samples, rotorAxes[n]);
                    for (int j = 0; j < positions.Length; j += 3)
                    {
                        bool blade = false;
                        for (int k = 0; k < 3; k++)
                            if (Vector3.ProjectOnPlane(positions[j + k], rotorAxes[n]).magnitude > 0.018f) blade = true;
                        if (!blade) continue;
                        for (int k = 0; k < 3; k++)
                        { blurFaces.Add(blurPositions.Count); blurPositions.Add(rotation * positions[j + k]); blurNormals.Add(rotation * rotorNormals[j + k]); }
                    }
                }
                var blur = new Mesh { name = "Rotor swept blade blur " + (n + 1) };
                blur.SetVertices(blurPositions); blur.SetNormals(blurNormals); blur.SetTriangles(blurFaces, 0); blur.RecalculateBounds();
                blurMeshes[n] = blur;
                var swept = new GameObject("High-speed blade blur"); swept.layer = rotor.layer; swept.transform.SetParent(rotor.transform, false);
                swept.AddComponent<MeshFilter>().sharedMesh = blur;
                blurRenderers[n] = swept.AddComponent<MeshRenderer>(); blurRenderers[n].sharedMaterial = blurMaterial;
                blurRenderers[n].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                blurRenderers[n].enabled = false;
            }
            vehicle = GetComponent<RovDynamics>();
        }

        private void OnEnable()
        {
            if (vehicle == null) return;
            vehicle.PhysicsStepped += AnimateRotors;
            vehicle.VehicleReset += ResetRotors;
        }
        private void OnDisable()
        {
            if (vehicle == null) return;
            vehicle.PhysicsStepped -= AnimateRotors;
            vehicle.VehicleReset -= ResetRotors;
        }
        private void AnimateRotors(float dt)
        {
            for (int i = 0; i < angles.Length; i++)
                angles[i] = Mathf.Repeat(angles[i] + RotorDegreesPerSecond(vehicle.Output(i)) * dt, 360);
        }
        private void ResetRotors() => System.Array.Clear(angles, 0, angles.Length);
        private void LateUpdate()
        {
            for (int i = 0; i < rotors.Length; i++)
                if (rotors[i] != null)
                {
                    rotors[i].localRotation = Quaternion.AngleAxis(angles[i], rotorAxes[i]);
                    blurRenderers[i].enabled = Mathf.Abs(vehicle.Output(i)) * VisualFullOutputRpm >= 900;
                }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < rotors.Length; i++)
            {
                if (rotors[i] != null) Destroy(rotors[i].gameObject);
                if (rotorMeshes[i] != null) Destroy(rotorMeshes[i]);
                if (blurMeshes[i] != null) Destroy(blurMeshes[i]);
            }
            if (blurMaterial != null) Destroy(blurMaterial);
            if (visual != null)
            {
                visual.sharedMesh = original;
                visual.GetComponent<MeshRenderer>().sharedMaterials = originalMaterials;
            }
            if (colored != null) Destroy(colored);
            if (palette != null) foreach (var material in palette) if (material != null) Destroy(material);
        }
    }
}
