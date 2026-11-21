using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ClubROV.Simulation.Editor
{
    public static class RovSensorLearningChecks
    {
        public static string CheckVisiblePractice()
        {
            var state = UnityEngine.Random.state;
            var body = new GameObject("Temporary practice camera check");
            try
            {
                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(body.transform, false);
                cameraObject.transform.localPosition = new Vector3(0, .04f, .31f);
                var view = cameraObject.AddComponent<Camera>();
                view.enabled = false;
                view.aspect = 84f / 64;
                view.fieldOfView = Camera.HorizontalToVerticalFieldOfView(48.7f, view.aspect);
                view.nearClipPlane = .03f;
                UnityEngine.Random.InitState(223);
                for (int i = 0; i < 2000; i++)
                {
                    bool dock = i % 2 == 0;
                    var target = new Vector3(UnityEngine.Random.Range(-4f, 4f),
                        dock ? UnityEngine.Random.Range(-3.1f, -2.2f) : UnityEngine.Random.Range(-1.8f, -.8f),
                        UnityEngine.Random.Range(8.5f, 10.3f));
                    var pose = RovSensorCourseAgent.VisiblePracticePose(target, dock ? 180 : 0,
                        2, .3f, .1f, 10, view, dock ? .5f : 0);
                    body.transform.SetPositionAndRotation(pose.position, pose.rotation);
                    var pixel = view.WorldToViewportPoint(target);
                    Require(pixel.z > view.nearClipPlane && pixel.x >= .05f && pixel.x <= .95f &&
                        pixel.y >= .05f && pixel.y <= .95f, "target inside actual camera projection");
                    Require(Mathf.Abs(pose.position.x) <= 8.5f && Mathf.Abs(pose.position.z) <= 10.8f &&
                        pose.position.y >= -3.2f && pose.position.y <= -.3f, "practice pose in pool");
                }
                return "Passed: 2000 hover/dock reset poses independently projected inside camera and pool bounds.";
            }
            finally { UnityEngine.Random.state = state; Object.DestroyImmediate(body); }
        }

        public static string CheckHoldPractice()
        {
            var root = new GameObject("Temporary unscored hold check");
            root.SetActive(false);
            try
            {
                var course = root.AddComponent<RovTrainingCourse>();
                Transform Target(string name, Vector3 position, float yaw)
                {
                    var target = new GameObject(name).transform;
                    target.SetParent(root.transform);
                    target.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
                    return target;
                }
                var hover = Target("hover", new Vector3(1, -1.5f, 2), 0);
                var dock = Target("dock", new Vector3(-1, -2.5f, 4), 180);
                course.Configure(new[] { Target("gate1", Vector3.zero, 0), Target("gate2", Vector3.forward, 0), Target("gate3", Vector3.forward * 2, 0) }, hover, dock);
                bool refused = false;
                try { course.BeginHoldPractice(false); } catch (InvalidOperationException) { refused = true; }
                Require(refused, "scored course refuses practice reset");
                course.RecordResults = false;
                course.BeginHoldPractice(false);
                Require(course.HoldingStage && course.GatesPassed == 3 && !course.HoverComplete && course.ObjectivesCompleted == 3, "hover practice stage");
                Require(course.CheckHold(hover.position, hover.rotation, Vector3.zero).Ready, "hover exact pose");
                Require(!course.CheckHold(hover.position, Quaternion.Euler(0, 30, 0), Vector3.zero).heading, "hover heading still required");
                Require(!course.CheckHold(hover.position + Vector3.right * .46f, hover.rotation, Vector3.zero).position, "hover radius unchanged");
                course.BeginHoldPractice(true);
                Require(course.HoldingStage && course.HoverComplete && course.ObjectivesCompleted == 4 && course.TargetPosition == dock.position, "dock practice stage");
                Require(course.CheckHold(dock.position, dock.rotation, Vector3.zero).Ready, "dock exact pose");
                Require(!course.CheckHold(dock.position, Quaternion.identity, Vector3.zero).heading, "dock wrong heading fails");
                Require(!course.CheckHold(dock.position + Vector3.up * .19f, dock.rotation, Vector3.zero).position, "dock depth tolerance unchanged");
                Require(!course.CheckHold(dock.position, dock.rotation, Vector3.forward * .11f).speed, "dock speed tolerance unchanged");
                course.ResetCourse();
                Require(course.GatesPassed == 0 && !course.HoverComplete && !course.HoldingStage, "normal course reset restored");
                for (int angle = -180; angle <= 180; angle += 15)
                    for (float speed = 0; speed <= 2; speed += .1f)
                    {
                        float potential = RovSensorCourseAgent.HoldPotential(angle, speed, 0);
                        Require(potential >= -.25001f && potential <= .50001f, "bounded hold potential");
                    }
                Require(RovSensorCourseAgent.HoldPotential(0, 0, 0) > RovSensorCourseAgent.HoldPotential(30, .2f, 0), "aligned slow hold preferred");
                return "Passed: unscored hover/dock reset, original heading/position/speed checks, normal reset and bounded shaping.";
            }
            finally { Object.DestroyImmediate(root); }
        }

        public static string CheckObservationBoundary()
        {
            var packet = new RovDvlSensor.Packet {
                stamp_ns = 9950000000, gyro = new[] { 0f, 0f, 1f }, accel = new[] { 0f, 0f, 9.81f },
                velocity = new[] { 1f, .1f, 0f }, depth = 1.2f, altitude = 2,
                valid = true, beams = 3 };
            var reading = new RovDvlSensor.Reading(packet);
            var output = new float[RovSensorObservationState.Count];
            RovSensorObservationState.Write(reading, reading, 10, new Vector3(.1f, 0, 0), output);
            Require(output.Length == 16, "vector size");
            Require(Mathf.Abs(output[6] - .3f) < 1e-6, "pressure normalization");
            Require(Mathf.Abs(output[7] - .5f) < 1e-6 && Mathf.Abs(output[8]) < 1e-6, "FLU lever correction");
            Require(output[11] == 1 && output[15] == 1, "fresh sensor masks");
            RovSensorObservationState.Write(reading, reading, 11, Vector3.zero, output);
            Require(output[11] == 0 && output[15] == 0 && output[7] == 0, "stale measurements masked");
            packet.valid = false;
            var invalid = new RovDvlSensor.Reading(packet);
            RovSensorObservationState.Write(reading, invalid, 10, Vector3.zero, output);
            Require(output[11] == 0 && output[7] == 0, "lost bottom lock masked");
            packet.valid = true; packet.gyro[0] = float.NaN;
            invalid = new RovDvlSensor.Reading(packet);
            RovSensorObservationState.Write(invalid, invalid, 10, Vector3.zero, output);
            Require(output[11] == 0 && output[15] == 0, "invalid sensor masks");
            foreach (float value in output) Require(float.IsFinite(value) && Mathf.Abs(value) <= 1, "finite bounded observation");
            return "Passed: 16 sensor-only scalars, FLU lever correction, freshness/dropout masks, finite measurement bounds.";
        }

        public static string CheckLocalSampling()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Local sampling check requires Play Mode.");
            var go = new GameObject("Temporary local sensor check");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                go.transform.position = new Vector3(1000, -1, 1000);
                var body = go.AddComponent<Rigidbody>(); body.isKinematic = true;
                go.AddComponent<BoxCollider>().size = new Vector3(.3f, .2f, .3f);
                var vehicle = go.AddComponent<RovDynamics>();
                var sensor = go.AddComponent<RovDvlSensor>();
                var settings = new SerializedObject(sensor);
                foreach (string field in new[] { "velocityNoiseStd", "gyroNoiseStd", "accelNoiseStd", "depthNoiseStd" })
                    settings.FindProperty(field).floatValue = 0;
                settings.ApplyModifiedPropertiesWithoutUndo();
                floor.transform.position = new Vector3(1000, -3, 1000);
                floor.transform.localScale = new Vector3(10, .1f, 10);
                Physics.SyncTransforms();
                sensor.StartLocalSampling(812);
                vehicle.CompletePhysicsStep(.02f);
                Require(sensor.LatestImu.Available && sensor.LatestDvl.Valid, "local IMU/DVL samples");
                Require(Mathf.Abs(sensor.LatestImu.Depth - 1) < 1e-6, "surface-zeroed pressure depth");
                Require(!sensor.Streaming && sensor.PacketsSent == 0, "no UDP in local sampling");
                bool refused = false;
                try { sensor.StartStreaming(); } catch (InvalidOperationException) { refused = true; }
                Require(refused, "local/ROS owner exclusion");
                sensor.StopLocalSampling();
                Require(!sensor.LocalSampling && !sensor.LatestImu.Available, "local cleanup");
                return "Passed: local noisy-measurement path, absolute pressure reference, DVL lock, no UDP, owner exclusion and cleanup.";
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(floor); }
        }

        private static void Require(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Sensor RL check failed: " + name);
        }
    }
}
