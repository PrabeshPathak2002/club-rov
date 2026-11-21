using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ClubROV.Simulation.Editor
{
    public static class RovTetherChecks
    {
        public static string Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Run in Play Mode.");
            var checks = new List<string>();
            void Check(string name, bool pass) { if (!pass) throw new Exception("FAIL: " + name); checks.Add("PASS: " + name); }
            RovTether.Load Load(Vector3 velocity, Vector3 current, float distance = 10, float deployed = 12, float drag = 0, float weight = 0) =>
                RovTether.CalculateLoad(new Vector3(0, -1, 0), new Vector3(distance, -1, 0), velocity, current,
                    RovTether.DefaultLength, deployed, 0.00569f, 0.033f, drag, weight);
            Check("100 ft default converts exactly to 30.48 m", Mathf.Abs(RovTether.DefaultLength - 100 * 0.3048f) < 0.00001f);
            var slack = Load(Vector3.zero, Vector3.zero);
            Check("Slack stationary cable produces no spring load", slack.force == Vector3.zero && slack.tension == 0);
            var taut = Load(Vector3.right, Vector3.zero, 31.48f);
            Check("Extension pulls toward bank and tension is capped", taut.force.x < 0 && taut.tension == 80 && taut.force.y == 0);
            var inward = Load(Vector3.left * 10, Vector3.zero, 30.49f);
            Check("Damping never creates compressive cable push", inward.tension == 0 && inward.force == Vector3.zero);
            var drag = Load(Vector3.forward, Vector3.zero, drag: 1);
            Check("Cable drag opposes transverse motion", drag.force.z < 0 && drag.force.x == 0);
            var current = Load(Vector3.zero, Vector3.forward * 0.3f, drag: 1);
            Check("Stationary cable is dragged with cross-current", current.force.z > 0);
            Check("Axial drag is excluded by this transverse model", Load(Vector3.right, Vector3.zero, drag: 1).force == Vector3.zero);
            Check("More deployed cable increases drag", Load(Vector3.forward, Vector3.zero, deployed: 20, drag: 1).force.magnitude > drag.force.magnitude);
            Check("Net cable weight pulls down", Load(Vector3.zero, Vector3.zero, weight: 1).force.y < 0);
            var buoyant = RovTether.CalculateLoad(Vector3.down, Vector3.right * 10 + Vector3.down, Vector3.zero, Vector3.zero,
                30.48f, 12, 0.006f, 0.01f, 0, 1);
            Check("Weight calculation supports a buoyant cable", buoyant.force.y > 0);
            var dry = RovTether.CalculateLoad(Vector3.up, Vector3.right * 10 + Vector3.up, Vector3.zero, Vector3.forward,
                30.48f, 12, 0.00569f, 0.033f, 1, 1);
            Check("Airborne cable has no water drag or submerged weight load", dry.force == Vector3.zero);
            var oldScenario = JsonUtility.FromJson<RovScenario>("{\"name\":\"Legacy preset\"}");
            oldScenario.Validate();
            Check("Legacy scenario loads new tether defaults", oldScenario.tetherEnabled && oldScenario.tetherLengthMetres == 30.48f);
            Check("Default and legacy-missing weight use neutral tether", new RovScenario().tetherWeightScale == 0 && oldScenario.tetherWeightScale == 0);
            string key = oldScenario.Key(); oldScenario.tetherEnabled = false;
            Check("Tether conditions separate score comparisons", key != oldScenario.Key());
            var roundTrip = JsonUtility.FromJson<RovScenario>(JsonUtility.ToJson(new RovScenario { tetherLengthMetres = 20, tetherDragScale = 2, tetherWeightScale = 0 }));
            Check("Scenario JSON preserves cable settings", roundTrip.tetherLengthMetres == 20 && roundTrip.tetherDragScale == 2 && roundTrip.tetherWeightScale == 0);
            bool rejected = false; roundTrip.tetherLengthMetres = float.NaN;
            try { roundTrip.Validate(); } catch (ArgumentException) { rejected = true; }
            Check("Nonfinite cable setting rejected", rejected);

            var live = UnityEngine.Object.FindAnyObjectByType<RovTrainingMaps>();
            int liveMap = live.Current; Vector3 livePosition = live.GetComponent<RovDynamics>().Body.position;
            var scene = SceneManager.CreateScene("Isolated tether validation", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var root = new GameObject("Tether test fixture"); root.SetActive(false); SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var environment = new GameObject("Test environment"); environment.transform.SetParent(root.transform);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Pool floor"; floor.transform.SetParent(environment.transform);
                floor.transform.position = new Vector3(0, -3.65f, 0); floor.transform.localScale = new Vector3(20.4f, 0.3f, 24.4f);
                var go = new GameObject("Test vehicle"); go.transform.SetParent(root.transform); go.transform.position = new Vector3(0, -1.2f, -1);
                var body = go.AddComponent<Rigidbody>(); body.mass = 8;
                var dynamics = go.AddComponent<RovDynamics>();
                var course = go.AddComponent<RovTrainingCourse>(); course.RecordResults = false;
                var maps = go.AddComponent<RovTrainingMaps>();
                maps.Configure(new[] { new RovMapLayout { root = environment, environment = environment, gates = Array.Empty<Transform>() } });
                root.SetActive(true); maps.Activate(0);
                var tether = go.GetComponent<RovTether>(); tether.ResetAnchor(); tether.Configure(true, 30.48f, 0, 0);
                Check("Launch anchor sits on nearest pool rim", tether.Ready && Mathf.Abs(tether.Anchor.x + 10) < 0.001f && tether.Anchor.y == 0.12f);
                Check("Initial payout includes slack and respects cable length", tether.PaidOutMetres > tether.DistanceMetres && tether.PaidOutMetres <= 30.48f);
                foreach (float step in new[] { 0.02f, 0.0025f })
                {
                    dynamics.ExternalPhysics = true;
                    dynamics.ResetVehicle();
                    body.position = new Vector3(0, -1.2f, -1); body.rotation = Quaternion.identity;
                    body.linearVelocity = body.angularVelocity = Vector3.zero;
                    var neutral = new RovScenario();
                    tether.Configure(neutral.tetherEnabled, neutral.tetherLengthMetres, neutral.tetherDragScale, neutral.tetherWeightScale);
                    tether.ResetAnchor(); dynamics.StopThrusters();
                    for (int n = 0; n < Mathf.RoundToInt(30 / step); n++)
                    { dynamics.ApplyForces(step); scene.GetPhysicsScene().Simulate(step); }
                    Check($"Neutral tether holds depth without thrust for 30 s at {1 / step:0} Hz",
                        Mathf.Abs(body.position.y + 1.2f) < 0.005f && Mathf.Abs(body.linearVelocity.y) < 0.001f && tether.LastForce.magnitude < 0.01f);
                }
                dynamics.ExternalPhysics = false;
                var line = go.GetComponentInChildren<LineRenderer>(true);
                Check("Cable visual has no collider or rigidbody", line != null && line.GetComponent<Collider>() == null && line.GetComponent<Rigidbody>() == null);
                float paid = tether.PaidOutMetres;
                body.position = new Vector3(-5, -1.2f, -1); tether.ApplyForces(0.02f);
                Check("Returning does not automatically reel in deployed cable", tether.PaidOutMetres == paid);
                // A short test cable exercises the limit without moving the user's ROV or global physics.
                tether.Configure(true, 3, 0, 0);
                Vector3 start = new(-5, -1.2f, -1);
                body.position = start; body.rotation = Quaternion.identity; body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
                dynamics.ApplyForces(0.02f); scene.GetPhysicsScene().Simulate(0.02f);
                Vector3 localVelocity = body.linearVelocity;
                Check("Tether force actually pulls the rigidbody toward shore", tether.TensionNewtons > 0 && localVelocity.x < 0);
                dynamics.ExternalPhysics = true;
                body.position = start; body.rotation = Quaternion.identity; body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
                dynamics.ApplyForces(0.02f); scene.GetPhysicsScene().Simulate(0.02f);
                Check("Externally owned step receives the same tether force", Vector3.Distance(localVelocity, body.linearVelocity) < 0.005f);
                body.position = start; body.rotation = Quaternion.identity; body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
                for (int i = 0; i < 2000; i++) { dynamics.ApplyForces(0.0025f); scene.GetPhysicsScene().Simulate(0.0025f); }
                Check("400 Hz spring integration remains finite and pulls inside the limit", float.IsFinite(body.position.x)
                    && float.IsFinite(body.position.y) && float.IsFinite(body.position.z) && tether.DistanceMetres < 3.5f && body.linearVelocity.magnitude < 3);
                tether.Configure(false, 3, 0, 0); tether.ApplyForces(0.02f);
                Check("Disabled tether clears forces and hides cable", tether.LastForce == Vector3.zero && tether.TensionNewtons == 0 && !line.enabled);
                dynamics.ExternalPhysics = false; tether.Configure(true, 30.48f, 0, 0);
                dynamics.SetStartPose(new Vector3(8, -1.2f, -1), Quaternion.identity);
                Check("Vehicle reset relocates anchor to new launch side", Mathf.Abs(tether.Anchor.x - 10) < 0.001f && tether.PaidOutMetres < 5 && tether.TensionNewtons == 0);
                tether.enabled = false; tether.enabled = true;
                Check("Re-enable reuses one cable visual", go.GetComponentsInChildren<LineRenderer>(true).Length == 1 && line.enabled);
                foreach (var layout in live.CopyLayouts())
                {
                    var env = layout.environment != null ? layout.environment : layout.root;
                    bool found = false;
                    foreach (var renderer in env.GetComponentsInChildren<MeshRenderer>(true))
                        if (renderer.name == "Pool floor" || renderer.name.StartsWith("Arena floor ")) found = true;
                    Check("Map environment exposes a tether anchor footprint: " + layout.root.name, found);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); SceneManager.UnloadSceneAsync(scene); }
            Check("Isolated physics preserved live map and vehicle", live.Current == liveMap && live.GetComponent<RovDynamics>().Body.position == livePosition);
            return string.Join("\n", checks);
        }
    }
}
