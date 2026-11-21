using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ClubROV.Simulation.Editor
{
    public static class RovDvlChecks
    {
        public static string Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Run DVL checks in Play Mode.");
            Near(RovDvlSensor.PolarFlu(Vector3.forward), Vector3.right, "forward FLU");
            Near(RovDvlSensor.PolarFlu(Vector3.right), Vector3.down, "starboard FLU");
            Near(RovDvlSensor.AxialFlu(Vector3.up), Vector3.back, "right turn yaw sign");
            Near(RovDvlSensor.VelocityAtSensor(Vector3.zero, new Vector3(0,0,-2), Quaternion.identity,
                new Vector3(0,0,-0.18f)), new Vector3(0,0.36f,0), "roll lever arm");
            Quaternion q = RovDvlSensor.RotationFlu(Quaternion.Euler(0,90,0));
            if (Mathf.Abs(q.z+Mathf.Sqrt(0.5f))>1e-5) throw new Exception("ROS quaternion yaw sign");
            var go = new GameObject("Temporary DVL check vehicle");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback,0));
            udp.Client.ReceiveTimeout=1000;
            try
            {
                go.transform.position=new Vector3(1000,-1,1000);
                var body=go.AddComponent<Rigidbody>(); body.isKinematic=true;
                go.AddComponent<BoxCollider>().size=new Vector3(0.3f,0.2f,0.3f);
                var dynamics=go.AddComponent<RovDynamics>();
                var sensor=go.AddComponent<RovDvlSensor>();
                var so=new SerializedObject(sensor);
                so.FindProperty("udpPort").intValue=((IPEndPoint)udp.Client.LocalEndPoint).Port;
                so.FindProperty("includeGroundTruth").boolValue=true;
                foreach(string field in new[]{"velocityNoiseStd","gyroNoiseStd","accelNoiseStd","depthNoiseStd"})
                    so.FindProperty(field).floatValue=0;
                so.ApplyModifiedPropertiesWithoutUndo();
                floor.name="Temporary DVL check floor";
                floor.transform.position=new Vector3(1000,-2,1000);
                floor.transform.localScale=new Vector3(10,0.2f,10);
                Physics.SyncTransforms();
                sensor.StartStreaming(); sensor.StartStreaming();
                RovDvlSensor.Packet Sample()
                {
                    dynamics.CompletePhysicsStep(0.11f);
                    IPEndPoint sender=null;
                    return JsonUtility.FromJson<RovDvlSensor.Packet>(Encoding.UTF8.GetString(udp.Receive(ref sender)));
                }
                var first=Sample();
                if (!first.valid || first.beams!=3 || Mathf.Abs(first.altitude-0.72f)>0.002f)
                    throw new Exception($"Three-beam bottom lock failed: {first.reason}, {first.altitude}");
                if (Mathf.Abs(first.accel[2]-9.81f)>0.01f) throw new Exception("IMU specific force must include gravity.");
                if (first.truth==null || first.truth.position[0]!=0 || first.truth.position[2]!=0)
                    throw new Exception("Launch truth origin failed.");
                if (!sensor.LatestImu.Available || !sensor.LatestDvl.Valid || sensor.LatestDvl.Beams != first.beams ||
                    sensor.LatestImu.Sequence != first.seq || Mathf.Abs(sensor.LatestImu.Acceleration.z - first.accel[2]) > 1e-6f)
                    throw new Exception("Debug readings differ from emitted sensor measurements.");
                dynamics.CompletePhysicsStep(0.02f);
                IPEndPoint diagnosticSender = null;
                udp.Receive(ref diagnosticSender);
                if (sensor.LatestDvl.Sequence != first.seq || sensor.LatestImu.Sequence <= first.seq)
                    throw new Exception("IMU-only sample overwrote cached DVL reading.");
                var second=Sample();
                if (second.seq<=first.seq || second.stamp_ns<=first.stamp_ns) throw new Exception("Nonmonotonic sample.");
                body.rotation=Quaternion.Euler(35,0,0); Physics.SyncTransforms();
                if (Sample().valid) throw new Exception("Excessive tilt retained bottom lock.");
                body.rotation=Quaternion.identity;
                floor.transform.position=new Vector3(1000,-1.4f,1000); Physics.SyncTransforms();
                if (Sample().valid) throw new Exception("Below-minimum altitude retained bottom lock.");
                floor.SetActive(false); Physics.SyncTransforms();
                if (Sample().valid) throw new Exception("Missing floor retained bottom lock.");
                floor.SetActive(true); floor.transform.position=new Vector3(1000,-2,1000); Physics.SyncTransforms();
                so.Update(); so.FindProperty("dropoutProbability").floatValue=1; so.ApplyModifiedPropertiesWithoutUndo();
                if (Sample().reason!="dropout") throw new Exception("Forced dropout failed.");
                dynamics.ResetVehicle();
                if (sensor.LatestImu.Available || sensor.LatestDvl.Available)
                    throw new Exception("Reset retained debug readings from the previous sensor session.");
                if (Sample().session==first.session) throw new Exception("Reset did not create a new sensor session.");
                sensor.enabled=false;
                if (sensor.Streaming) throw new Exception("Disable leaked sensor socket.");
                return "18 DVL coordinate, lever-arm, clock, bottom-lock, dropout/reset, diagnostic snapshot and lifecycle checks passed.";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(floor);
                Physics.SyncTransforms();
            }
        }
        private static void Near(Vector3 actual,Vector3 expected,string label)
        { if ((actual-expected).magnitude>1e-5f) throw new Exception(label+": "+actual); }
    }
}
