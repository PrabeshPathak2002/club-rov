using System;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ClubROV.Simulation.Editor
{
    public static class RovLearningChecks
    {
        public static string CheckRandomisation()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play before RL checks.");
            var original = Object.FindAnyObjectByType<RovTrainingMaps>().CopyLayouts()[0];
            var scene = EditorSceneManager.NewPreviewScene();
            var randomState = UnityEngine.Random.state;
            try
            {
                var layout = new RovMapLayout { gates = new Transform[3] };
                var originals = new[] { original.gates[0], original.gates[1], original.gates[2], original.hover, original.dock };
                var copies = new Transform[5];
                for (int i = 0; i < 5; i++)
                {
                    var copy = Object.Instantiate(originals[i].gameObject);
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy, scene);
                    copies[i] = copy.transform;
                    copies[i].SetPositionAndRotation(originals[i].position, originals[i].rotation);
                    if (i < 3) layout.gates[i] = copies[i];
                }
                layout.hover = copies[3]; layout.dock = copies[4];
                var positions = Array.ConvertAll(copies, t => t.position);
                var rotations = Array.ConvertAll(copies, t => t.rotation);
                var scales = Array.ConvertAll(copies, t => t.localScale);
                var randomizer = new RovCourseRandomizer(layout);
                var full = new RovCourseRandomizer.Ranges((name, fallback) => fallback);
                UnityEngine.Random.InitState(9187);
                for (int iteration = 0; iteration < 2000; iteration++)
                {
                    randomizer.Randomize(full);
                    for (int i = 0; i < 5; i++)
                    {
                        Vector3 offset = copies[i].position - positions[i];
                        Require(Mathf.Abs(offset.x) <= 1.001f && Mathf.Abs(offset.y) <= 0.401f && Mathf.Abs(offset.z) <= 0.751f, "Layout range");
                        Require(Quaternion.Angle(copies[i].rotation, rotations[i]) <= 15.01f, "Yaw range");
                        Require(copies[i].localScale == scales[i], "Scale preservation");
                        foreach (var collider in copies[i].GetComponentsInChildren<BoxCollider>(true))
                        {
                            for (int corner = 0; corner < 8; corner++)
                            {
                                var local = collider.center + Vector3.Scale(collider.size * 0.5f,
                                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                                Vector3 point = collider.transform.TransformPoint(local);
                                Require(Mathf.Abs(point.x) < 10 && Mathf.Abs(point.z) < 12 && point.y >= -3.5f && point.y <= 0, "Pool clearance");
                            }
                        }
                    }
                    Require(copies[1].position.z > copies[0].position.z + 1 && copies[2].position.z > copies[1].position.z + 1, "Gate ordering");
                }
                randomizer.Restore();
                for (int i = 0; i < 5; i++)
                    Require(copies[i].position == positions[i] && Quaternion.Angle(copies[i].rotation, rotations[i]) < 0.01f && copies[i].localScale == scales[i], "Restore");
                var fixedRanges = new RovCourseRandomizer.Ranges((name, fallback) => name == "course_randomisation" ? 0 : fallback);
                randomizer.Randomize(fixedRanges);
                for (int i = 0; i < 5; i++) Require(copies[i].position == positions[i], "Fixed layout");
                Require(fixedRanges.StartLateral == 0.15f && fixedRanges.StartDepth == 0.08f && fixedRanges.StartHeading == 5, "Legacy jitter");
                return "Passed 2000 random layouts: range, pool clearance, gate order, scales, restore and fixed profile.";
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void Require(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("RL randomisation check failed: " + label);
        }
    }
}
