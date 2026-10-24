using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEditor;
using UnityEngine;

namespace ClubROV.Simulation.Editor
{
    public static class RovSitlChecks
    {
        public static string Protocol()
        {
            int checks = 0;
            void Check(bool pass, string name) { if (!pass) throw new Exception(name); checks++; }
            var pwm = new ushort[6]; var packet = new byte[40];
            packet[0] = 0x1A; packet[1] = 0x48; packet[2] = 0x90; packet[3] = 1; packet[4] = 73;
            for (int i = 0; i < 6; i++) { packet[8 + i * 2] = 0xDC; packet[9 + i * 2] = 5; }
            Check(RovSitlProtocol.Decode(packet, 40, pwm, out uint frame, out ushort rate) && frame == 73 && rate == 400 && pwm[5] == 1500, "servo decoding");
            Check(!RovSitlProtocol.Decode(packet, 39, pwm, out _, out _), "truncated packet rejected");
            packet[0] = 0; Check(!RovSitlProtocol.Decode(packet, 40, pwm, out _, out _), "wrong magic rejected");
            Check(RovSitlProtocol.Pwm(0) == 0 && RovSitlProtocol.Pwm(1001) == -1, "invalid/disarmed range");
            Check(RovSitlProtocol.Pwm(1500) == 0 && RovSitlProtocol.Pwm(1525) == 0, "neutral deadband");
            Check(RovSitlProtocol.Pwm(1900) == 1 && RovSitlProtocol.Pwm(1100) == -1, "PWM endpoints");
            Check((RovSitlProtocol.Polar(new Vector3(2, -3, 1)) - new Vector3(1, 2, 3)).sqrMagnitude < 1e-6, "position NED");
            Check((RovSitlProtocol.Axial(Vector3.up) - Vector3.forward).sqrMagnitude < 1e-6, "right yaw gyro sign");
            Check(Mathf.Abs(RovSitlProtocol.Euler(Quaternion.Euler(0, 90, 0)).z - Mathf.PI / 2) < 1e-5, "east heading");
            Check(Mathf.Abs(RovSitlProtocol.Euler(Quaternion.Euler(-30, 0, 0)).y - Mathf.PI / 6) < 1e-5, "nose-up pitch");
            Check(Mathf.Abs(RovSitlProtocol.Euler(Quaternion.Euler(0, 0, -30)).x - Mathf.PI / 6) < 1e-5, "starboard-down roll");
            string json = System.Text.Encoding.ASCII.GetString(RovSitlProtocol.Sensors(1, Vector3.down, Quaternion.identity,
                Vector3.zero, Vector3.zero, Vector3.up * 9.81f));
            Check(json.StartsWith("\n{") && json.EndsWith("}\n") && json.Contains("[0.000000,0.000000,-9.810000]"), "specific force and framing");
            return $"{checks} SITL protocol checks passed";
        }

        private static RovSitlBridge bridge;
        private static UdpClient client;
        private static double deadline;
        private static long firstSteps;
        private static int stage;
        private static readonly List<string> results = new();
        public static string StartLifecycle()
        {
            if (!EditorApplication.isPlaying) throw new Exception("Enter Play Mode first.");
            bridge = UnityEngine.Object.FindAnyObjectByType<RovSitlBridge>();
            if (bridge == null || bridge.Active) throw new Exception("Bridge must exist and be disconnected.");
            results.Clear(); stage = 0;
            bridge.Configure("127.0.0.1"); bridge.Connect();
            client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            Send(1); deadline = EditorApplication.timeSinceStartup + 1;
            EditorApplication.update += Tick;
            return "SITL lifecycle checks running";
        }
        private static void Send(uint frame)
        {
            var packet = new byte[40]; packet[0] = 0x1A; packet[1] = 0x48; packet[2] = 0x90; packet[3] = 1;
            Array.Copy(BitConverter.GetBytes(frame), 0, packet, 4, 4);
            for (int i = 0; i < 6; i++) { packet[8 + i * 2] = 0xDC; packet[9 + i * 2] = 5; }
            client.Send(packet, packet.Length, new IPEndPoint(IPAddress.Loopback, 9002));
        }
        private static void Require(bool pass, string label) { if (!pass) throw new Exception(label); results.Add(label); }
        private static void Tick()
        {
            try
            {
                if (stage == 0 && bridge.Steps > 0)
                {
                    Require(Physics.simulationMode == SimulationMode.Script && !bridge.GetComponent<RovPilotInput>().enabled, "exclusive physics/input ownership");
                    Require(client.Available > 0, "UDP sensor reply received");
                    IPEndPoint source = null; string reply = System.Text.Encoding.ASCII.GetString(client.Receive(ref source));
                    Require(reply.Contains("\"imu\"") && reply.Contains("\"position\""), "sensor fields present");
                    firstSteps = bridge.Steps; Send(1); deadline = EditorApplication.timeSinceStartup + 0.2; stage = 1;
                }
                else if (stage == 1 && EditorApplication.timeSinceStartup > deadline)
                {
                    Require(bridge.Steps == firstSteps && client.Available > 0, "duplicate re-replied without physics advance");
                    deadline = EditorApplication.timeSinceStartup + 1.2; stage = 2;
                }
                else if (stage == 2 && EditorApplication.timeSinceStartup > deadline)
                {
                    Require(bridge.Status.Contains("link lost"), "lost-link watchdog");
                    var vehicle = bridge.GetComponent<RovDynamics>();
                    for (int i = 0; i < 6; i++) Require(vehicle.Output(i) == 0, "timeout clears actuator " + i);
                    bridge.Disconnect();
                    Require(Physics.simulationMode == SimulationMode.FixedUpdate && bridge.GetComponent<RovPilotInput>().enabled, "local ownership restored");
                    Finish(null);
                }
                else if (stage == 0 && EditorApplication.timeSinceStartup > deadline) throw new Exception("No physics reply within 1s");
            }
            catch (Exception error) { Finish(error.Message); }
        }
        private static void Finish(string error)
        {
            EditorApplication.update -= Tick; client?.Dispose(); bridge?.Disconnect();
            string text = error == null ? $"PASS: {results.Count} SITL lifecycle checks\n" + string.Join("\n", results) : "FAIL: " + error;
            File.WriteAllText("SitlCheckResults.txt", text);
            if (error == null) Debug.Log(text); else Debug.LogError(text);
        }
    }
}
