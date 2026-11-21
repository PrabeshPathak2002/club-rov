using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ClubROV.Simulation.Editor
{
    public static class RovRotorChecks
    {
        public static string Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run rotor checks in Play Mode.");
            var checks = new List<string>();
            void Check(string name, bool value) { if (!value) throw new Exception(name); checks.Add("PASS: " + name); }
            var go = new GameObject("Temporary rotor validation"); go.SetActive(false);
            var visual = new GameObject("September assembly visual"); visual.transform.SetParent(go.transform, false);
            var source = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ROV/Generated/ClubROV.asset");
            visual.AddComponent<MeshFilter>().sharedMesh = source;
            var live = UnityEngine.Object.FindAnyObjectByType<RovAppearance>();
            visual.AddComponent<MeshRenderer>().sharedMaterial = live.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            var body = go.AddComponent<Rigidbody>(); body.isKinematic = true;
            var dynamics = go.AddComponent<RovDynamics>();
            try
            {
                go.SetActive(true); go.GetComponent<RovAudio>().enabled = false;
                var appearance = go.GetComponent<RovAppearance>(); dynamics.ExternalPhysics = true;
                int triangles = visual.GetComponent<MeshFilter>().sharedMesh.triangles.Length;
                for (int i = 0; i < 6; i++)
                {
                    var rotor = visual.transform.Find("Thruster rotor " + (i + 1));
                    Check("Rotor " + (i + 1) + " has its own 882-triangle mesh", rotor != null && rotor.GetComponent<MeshFilter>().sharedMesh.triangles.Length == 882 * 3);
                    triangles += rotor.GetComponent<MeshFilter>().sharedMesh.triangles.Length;
                    var shaft = RovThrusterLayout.Direction(i);
                    Check("Rotor " + (i + 1) + " pivot lies on CAD shaft", Vector3.ProjectOnPlane(rotor.localPosition - RovThrusterLayout.Position(i), shaft).magnitude < 0.00001f);
                    float radius = 0;
                    foreach (var point in rotor.GetComponent<MeshFilter>().sharedMesh.vertices)
                        for (int angle = 0; angle < 360; angle += 15)
                        {
                            Vector3 rotated = Quaternion.AngleAxis(angle, shaft) * point;
                            radius = Mathf.Max(radius, Vector3.ProjectOnPlane(rotated + rotor.localPosition - RovThrusterLayout.Position(i), shaft).magnitude);
                        }
                    Check("Rotor " + (i + 1) + " full sweep stays inside 31 mm housing bore", radius < 0.031f);
                    dynamics.StopThrusters();
                    dynamics.SetActuator(i, 0.5f); dynamics.ApplyForces(0.2f);
                    Quaternion before = rotor.localRotation;
                    dynamics.CompletePhysicsStep(0.003f); appearance.SendMessage("LateUpdate");
                    Check("Rotor " + (i + 1) + " half thrust rotates 27 degrees over 3 ms", Mathf.Abs(Quaternion.Angle(before, rotor.localRotation) - 27) < 0.01f);
                    var rotation = rotor.localRotation;
                    dynamics.StopThrusters(); dynamics.CompletePhysicsStep(0.003f); appearance.SendMessage("LateUpdate");
                    Check("Rotor " + (i + 1) + " stops at zero output", Quaternion.Angle(rotation, rotor.localRotation) < 0.01f);
                    Check("Rotor " + (i + 1) + " blur disappears at zero thrust", !rotor.Find("High-speed blade blur").GetComponent<MeshRenderer>().enabled);
                }
                Check("Original triangle count is conserved", triangles == source.triangles.Length);
                Check("Rotors add no physics components", visual.GetComponentsInChildren<Collider>().Length == 0 && visual.GetComponentsInChildren<Rigidbody>().Length == 0);
                Check("Spin rate is linear and signed", RovAppearance.RotorDegreesPerSecond(0.5f) * 2 == RovAppearance.RotorDegreesPerSecond(1) && RovAppearance.RotorDegreesPerSecond(-1) == -RovAppearance.RotorDegreesPerSecond(1));
                var first = visual.transform.Find("Thruster rotor 1");
                var originalRotation = first.localRotation;
                dynamics.SetThrusterHealth(0, 0); dynamics.SetActuator(0, 1); dynamics.ApplyForces(0.2f);
                dynamics.CompletePhysicsStep(0.1f); appearance.SendMessage("LateUpdate");
                Check("Failed rotor stays stopped", Quaternion.Angle(originalRotation, first.localRotation) < 0.01f);
                dynamics.SetThrusterHealth(0, 1); dynamics.StopThrusters(); dynamics.SetActuator(0, -0.5f); dynamics.ApplyForces(0.2f);
                dynamics.CompletePhysicsStep(0.003f); appearance.SendMessage("LateUpdate");
                var expected = originalRotation * Quaternion.AngleAxis(-21.6f, RovThrusterLayout.Direction(0));
                Check("Reverse uses actual reverse-limited output and opposite rotation", Quaternion.Angle(expected, first.localRotation) < 0.01f);
                appearance.enabled = false; appearance.enabled = true;
                originalRotation = first.localRotation;
                dynamics.CompletePhysicsStep(0.003f); appearance.SendMessage("LateUpdate");
                Check("Re-enable has one animation subscription", Mathf.Abs(Quaternion.Angle(originalRotation, first.localRotation) - 21.6f) < 0.01f);
                dynamics.ExternalPhysics = false; dynamics.ResetVehicle(); appearance.SendMessage("LateUpdate");
                Check("Vehicle reset clears rotor angles", Quaternion.Angle(first.localRotation, Quaternion.identity) < 0.01f);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            return string.Join("\n", checks);
        }
    }
}
