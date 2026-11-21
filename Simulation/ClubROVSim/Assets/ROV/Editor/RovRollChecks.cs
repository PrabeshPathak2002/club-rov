using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ClubROV.Simulation.Editor
{
    public static class RovRollChecks
    {
        // An isolated physics scene avoids moving the training vehicle or recording a run.
        public static string Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Run roll checks in Play Mode.");
            var observations = new List<string>();
            var outputs = new float[6];
            foreach (float roll in new[] { -1f, 1f })
            {
                RovThrusterLayout.Mix(Vector3.zero, 0, outputs, roll);
                Vector3 force = Vector3.zero, torque = Vector3.zero;
                for (int i = 0; i < 6; i++)
                {
                    Vector3 f = RovThrusterLayout.Direction(i) * outputs[i];
                    force += f;
                    torque += Vector3.Cross(RovThrusterLayout.Position(i), f);
                }
                if (force.magnitude > 0.0001f || torque.z * roll >= -0.3f)
                    throw new InvalidOperationException("Roll must produce the requested torque without net translation.");
            }
            RovThrusterLayout.Mix(Vector3.up, 1, outputs, 1);
            foreach (float output in outputs)
                if (Mathf.Abs(output) > 1.0001f) throw new InvalidOperationException("Combined-axis saturation failed.");

            foreach (float limit in new[] { 0.6f, 1f })
            foreach (float roll in new[] { -1f, 1f })
            {
                Scene scene = SceneManager.CreateScene("Roll check " + Guid.NewGuid().ToString("N"), new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                var go = new GameObject("Isolated roll vehicle");
                SceneManager.MoveGameObjectToScene(go, scene);
                try
                {
                    go.transform.position = new Vector3(0, -1.2f, 0);
                    var body = go.AddComponent<Rigidbody>();
                    body.mass = 8;
                    go.AddComponent<BoxCollider>().size = RovThrusterLayout.EnvelopeMetres;
                    var vehicle = go.AddComponent<RovDynamics>();
                    vehicle.SetPilotCommand(Vector3.zero, 0, limit, roll);
                    vehicle.ExternalPhysics = true;
                    var physics = scene.GetPhysicsScene();
                    const float dt = 0.01f;
                    float rotation = 0, maximumTilt = 0;
                    for (int step = 0; step < 3000; step++)
                    {
                        vehicle.ApplyForces(dt);
                        physics.Simulate(dt);
                        rotation -= Vector3.Dot(body.angularVelocity, body.rotation * Vector3.forward) * dt * Mathf.Rad2Deg;
                        maximumTilt = Mathf.Max(maximumTilt, Vector3.Angle(Vector3.up, body.rotation * Vector3.up));
                    }
                    if (rotation * roll <= 5 || (limit == 1 && rotation * roll < 360))
                        throw new InvalidOperationException($"Roll motion check failed: limit={limit}, roll={roll}, rotation={rotation}.");
                    observations.Add($"limit={limit:F1}, command={roll:F0}: rotation={rotation:F1} deg, max tilt={maximumTilt:F1} deg");
                    vehicle.StopThrusters();
                    for (int i = 0; i < 6; i++)
                        if (vehicle.Output(i) != 0) throw new InvalidOperationException("Thrust cut left roll output active.");
                    vehicle.ExternalPhysics = false;
                    vehicle.ResetVehicle();
                    vehicle.ExternalPhysics = true;
                    vehicle.SetPilotCommand(Vector3.zero, 0, 1, 1);
                    vehicle.ApplyForces(dt);
                    for (int i = 0; i < 6; i++)
                        if (vehicle.Output(i) != 0) throw new InvalidOperationException("Local roll bypassed SITL ownership.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(go);
                    SceneManager.UnloadSceneAsync(scene);
                }
            }
            return "Roll force/saturation, four isolated 30-second physics runs, thrust-cut and SITL ownership checks passed. "
                + string.Join("; ", observations);
        }
    }
}
