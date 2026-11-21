using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClubROV.Simulation
{
    // Presentation only: existing pool colliders and sensor raycasts are untouched.
    public sealed class RovPoolAppearance : IDisposable
    {
        private readonly List<(Renderer renderer, Material[] materials, bool enabled)> originals = new();
        private readonly List<GameObject> additions = new();
        private readonly List<Material> ownedMaterials = new();

        public RovPoolAppearance(UnityEngine.SceneManagement.Scene scene)
        {
            Material Make(string resource)
            {
                var source = Resources.Load<Material>(resource);
                if (source == null) throw new InvalidOperationException("Missing pool material: " + resource);
                var copy = new Material(source);
                ownedMaterials.Add(copy);
                return copy;
            }
            var floorMaterial = Make("PoolCeramicFloor");
            var wallMaterial = Make("PoolCeramicWall");
            var arenaMaterial = Make("PoolCeramicArena");
            var surfaceMaterial = Make("PoolWaterSurface");
            var copingMaterial = Make("PoolCoping");
            var steelMaterial = Make("PoolSteel");
            foreach (var root in scene.GetRootGameObjects())
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                string name = renderer.name;
                bool floor = name == "Pool floor" || name.StartsWith("Arena floor ", StringComparison.Ordinal);
                bool wall = name == "Port wall" || name == "Starboard wall" || name == "Far wall" || name == "Near wall"
                    || name == "West wall" || name == "East wall" || name == "North wall" || name == "South wall";
                bool grid = name == "Grid X" || name == "Grid Z";
                if (!floor && !wall && !grid) continue;
                originals.Add((renderer, renderer.sharedMaterials, renderer.enabled));
                if (grid) { renderer.enabled = false; continue; }
                renderer.sharedMaterial = wall ? wallMaterial : name == "Pool floor" ? floorMaterial : arenaMaterial;
                if (!floor) continue;
                Bounds bounds = renderer.bounds;
                var dressing = new GameObject("Pool visual details");
                dressing.transform.SetParent(renderer.transform.parent, true);
                additions.Add(dressing);
                float x = bounds.center.x, z = bounds.center.z;
                float width = bounds.size.x - 0.4f, length = bounds.size.z - 0.4f;
                var surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
                surface.name = "Rippling pool surface";
                surface.transform.SetParent(dressing.transform, false);
                surface.transform.SetPositionAndRotation(new Vector3(x, -0.015f, z), Quaternion.Euler(90, 0, 0));
                surface.transform.localScale = new Vector3(width, length, 1);
                UnityEngine.Object.Destroy(surface.GetComponent<Collider>());
                surface.GetComponent<Renderer>().sharedMaterial = surfaceMaterial;
                surface.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                void Box(string label, Vector3 centre, Vector3 size, Material material)
                {
                    var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    item.name = label;
                    item.transform.SetParent(dressing.transform, false);
                    item.transform.position = centre;
                    item.transform.localScale = size;
                    UnityEngine.Object.Destroy(item.GetComponent<Collider>());
                    item.GetComponent<Renderer>().sharedMaterial = material;
                    item.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
                // A broad deck and narrow coping lip establish the actual water level.
                foreach (float side in new[] { -1f, 1f })
                {
                    Box("Pool deck side", new Vector3(x + side * (width / 2 + 0.6f), 0.06f, z), new Vector3(1.2f, 0.12f, length + 2.4f), copingMaterial);
                    Box("Pool deck end", new Vector3(x, 0.06f, z + side * (length / 2 + 0.6f)), new Vector3(width, 0.12f, 1.2f), copingMaterial);
                    Box("Coping side", new Vector3(x + side * width / 2, 0.035f, z), new Vector3(0.20f, 0.07f, length), copingMaterial);
                    Box("Coping end", new Vector3(x, 0.035f, z + side * length / 2), new Vector3(width, 0.07f, 0.20f), copingMaterial);
                }
                // Stainless ladder beside the wall, with no new collision or navigation surfaces.
                float ladderX = x + width / 2 - 0.12f, ladderZ = z - length / 2 + 3;
                foreach (float side in new[] { -1f, 1f })
                    Box("Ladder rail", new Vector3(ladderX, -0.5f, ladderZ + side * 0.26f), new Vector3(0.035f, 1.8f, 0.035f), steelMaterial);
                for (int step = 0; step < 4; step++)
                    Box("Ladder rung", new Vector3(ladderX - 0.06f, -0.25f - step * 0.3f, ladderZ), new Vector3(0.13f, 0.035f, 0.55f), steelMaterial);
            }
        }

        public void Dispose()
        {
            foreach (var item in originals)
                if (item.renderer != null) { item.renderer.sharedMaterials = item.materials; item.renderer.enabled = item.enabled; }
            foreach (var item in additions) if (item != null) UnityEngine.Object.Destroy(item);
            foreach (var item in ownedMaterials) if (item != null) UnityEngine.Object.Destroy(item);
            originals.Clear(); additions.Clear(); ownedMaterials.Clear();
        }
    }
}
