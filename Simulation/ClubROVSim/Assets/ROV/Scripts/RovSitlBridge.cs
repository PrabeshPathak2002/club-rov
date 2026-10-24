using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace ClubROV.Simulation
{
    [RequireComponent(typeof(RovDynamics), typeof(RovPilotInput))]
    public sealed class RovSitlBridge : MonoBehaviour
    {
        [SerializeField] private string simulatorAddress = "127.0.0.1";
        [SerializeField] private int listenPort = 9002;
        private RovDynamics vehicle;
        private RovPilotInput pilot;
        private Socket socket;
        private IPAddress allowed;
        private IPEndPoint peer;
        private readonly byte[] receive = new byte[256];
        private readonly ushort[] pwm = new ushort[6];
        private readonly Stopwatch clock = new();
        private byte[] lastReply;
        private uint lastFrame;
        private bool hasFrame, pilotWasEnabled, backgroundWasEnabled;
        private SimulationMode previousMode;
        private Vector3 origin;
        private double lastPacket, startedAt, simulated;
        public bool Active => socket != null;
        public long Steps { get; private set; }
        public double SimulatedSeconds => simulated;
        public string Status { get; private set; } = "LOCAL · Unity pilot";
        public string SimulatorAddress => simulatorAddress;

        private void Awake() { vehicle = GetComponent<RovDynamics>(); pilot = GetComponent<RovPilotInput>(); }
        public void Configure(string address)
        {
            if (Active) throw new InvalidOperationException("Disconnect before changing the simulator address.");
            if (!IPAddress.TryParse(address, out var parsed) || parsed.AddressFamily != AddressFamily.InterNetwork)
                throw new ArgumentException("Use the WSL IPv4 address or 127.0.0.1.");
            simulatorAddress = parsed.ToString();
        }
        public void Connect()
        {
            if (Active) return;
            if (Physics.simulationMode != SimulationMode.FixedUpdate)
                throw new InvalidOperationException("Another component owns the physics clock.");
            allowed = IPAddress.Parse(simulatorAddress);
            var next = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            try { next.Bind(new IPEndPoint(IPAddress.Any, listenPort)); next.Blocking = false; }
            catch { next.Dispose(); throw; }
            pilotWasEnabled = pilot.enabled; pilot.enabled = false;
            vehicle.StopThrusters(); vehicle.ExternalPhysics = true;
            previousMode = Physics.simulationMode; Physics.simulationMode = SimulationMode.Script;
            backgroundWasEnabled = Application.runInBackground; Application.runInBackground = true;
            origin = new Vector3(vehicle.Body.position.x, 0, vehicle.Body.position.z);
            simulated = 0; Steps = 0; hasFrame = false; peer = null; lastReply = null;
            clock.Restart(); lastPacket = 0; socket = next;
            Status = $"SITL · waiting for {allowed}:{listenPort}";
        }
        public void Disconnect()
        {
            if (!Active) return;
            socket.Dispose(); socket = null; clock.Stop();
            vehicle.StopThrusters(); vehicle.ExternalPhysics = false;
            Physics.simulationMode = previousMode; pilot.enabled = pilotWasEnabled;
            Application.runInBackground = backgroundWasEnabled;
            Status = "LOCAL · Unity pilot";
        }
        private void OnDisable() => Disconnect();
        private void Update()
        {
            if (!Active) return;
            double now = clock.Elapsed.TotalSeconds;
            if (hasFrame && now - lastPacket > 1)
            {
                vehicle.StopThrusters();
                Status = "SITL · link lost, thrust cleared · reconnect both ends";
                return; // Hold physics; require a coordinated restart, never replay stale thrust.
            }
            double budget = now + 0.010;
            try
            {
                for (int i = 0; i < 64 && clock.Elapsed.TotalSeconds < budget; i++)
                {
                    if (hasFrame && simulated >= clock.Elapsed.TotalSeconds - startedAt) break;
                    if (!socket.Poll(100, SelectMode.SelectRead)) break;
                    EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    int count = socket.ReceiveFrom(receive, ref sender);
                    var endpoint = (IPEndPoint)sender;
                    if (!endpoint.Address.Equals(allowed) || (peer != null && !peer.Equals(endpoint))) continue;
                    if (!RovSitlProtocol.Decode(receive, count, pwm, out uint frame, out ushort rate)) continue;
                    if (hasFrame && frame == lastFrame)
                    {
                        socket.SendTo(lastReply, endpoint); continue; // Retransmit without stepping twice.
                    }
                    if (hasFrame && frame < lastFrame)
                    {
                        vehicle.StopThrusters(); Status = "SITL · simulator restarted · reconnect both ends";
                        lastPacket = double.NegativeInfinity; return;
                    }
                    if (!hasFrame) { peer = endpoint; startedAt = clock.Elapsed.TotalSeconds; }
                    float dt = 1f / rate;
                    Vector3 before = vehicle.Body.linearVelocity;
                    RovSitlProtocol.Apply(pwm, vehicle);
                    vehicle.ApplyForces(dt); Physics.Simulate(dt);
                    simulated += dt; Steps++;
                    Vector3 force = (vehicle.Body.linearVelocity - before) / dt - Physics.gravity;
                    lastReply = RovSitlProtocol.Sensors(simulated, vehicle.Body.position - origin,
                        vehicle.Body.rotation, vehicle.Body.linearVelocity, vehicle.Body.angularVelocity, force);
                    socket.SendTo(lastReply, endpoint);
                    lastFrame = frame; hasFrame = true; lastPacket = clock.Elapsed.TotalSeconds;
                    Status = $"SITL · {rate} Hz physics · {simulated:0.0} s · ArduSub owns thrust";
                }
            }
            catch (SocketException error)
            {
                Disconnect(); Status = $"SITL socket closed · {error.SocketErrorCode}";
                UnityEngine.Debug.LogWarning(Status);
            }
        }
    }
}
