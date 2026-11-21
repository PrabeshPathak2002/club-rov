using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

namespace ClubROV.Simulation
{
    public sealed class RovMappingFeed : MonoBehaviour
    {
        private const int Width = 320, Height = 180, Port = 9013;
        private const double CaptureInterval = 1.0 / 30;
        // WSL's localhost relay keeps refused connections half-open; retrying at frame rate exhausts it.
        private const double RetryInterval = 2.0;
        private RovDvlSensor sensor;
        private Camera view;
        private RTHandle rgb, depth;
        private Material depthMaterial;
        private bool queued, reading;
        private Task send;
        private CancellationTokenSource lifetime;
        private double nextCapture;
        private long lastStamp;
        private string lastSession;
        public string Status { get; private set; } = "Mapping off";
        public void Configure(RovDvlSensor source) => sensor = source;

        private void OnEnable()
        {
            view = GetComponent<Camera>();
            lifetime = new CancellationTokenSource();
            RenderPipelineManager.endCameraRendering += ReadFrame;
        }

        private void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= ReadFrame;
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;
            queued = false;
            // Readbacks hold the textures until both callbacks complete.
            if (!reading) Release();
            Status = "Mapping off";
        }

        private void Release()
        {
            rgb?.Release(); rgb = null;
            depth?.Release(); depth = null;
            CoreUtils.Destroy(depthMaterial); depthMaterial = null;
        }

        public void Record(RenderGraph graph, TextureHandle color, TextureHandle cameraDepth)
        {
            if (!isActiveAndEnabled || reading || queued || send is { IsCompleted: false }) return;
            if (send is { IsCompleted: true })
            {
                bool failed = send.IsFaulted;
                Status = failed ? "Mapping receiver unavailable · TCP 9013" : "RGB-D streaming";
                _ = send.Exception;
                send = null;
                if (failed) { nextCapture = Time.realtimeSinceStartupAsDouble + RetryInterval; return; }
            }
            if (sensor == null || !sensor.Streaming || !sensor.LatestImu.Available)
            { Status = "Start the ROS sensor stream"; return; }
            if (!SystemInfo.supportsAsyncGPUReadback || !cameraDepth.IsValid())
            { Status = "Mapping depth/readback unavailable"; return; }
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextCapture || now - sensor.LatestImu.WallTime > 0.25) return;
            long stamp = (long)Math.Round(sensor.LatestImu.Seconds * 1e9);
            if (lastSession == sensor.Session && stamp <= lastStamp) return;
            if (depthMaterial == null)
            {
                var shader = Resources.Load<Shader>("RovMappingDepth");
                if (shader == null) { Status = "Mapping depth shader missing"; return; }
                depthMaterial = CoreUtils.CreateEngineMaterial(shader);
            }
            rgb ??= RTHandles.Alloc(Width, Height, colorFormat: GraphicsFormat.R8G8B8A8_SRGB, name: "ROV mapping color");
            depth ??= RTHandles.Alloc(Width, Height, colorFormat: GraphicsFormat.R32_SFloat, name: "ROV mapping depth");
            graph.AddBlitPass(color, graph.ImportTexture(rgb), Vector2.one, Vector2.zero, passName: "ROV mapping color");
            graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(cameraDepth, graph.ImportTexture(depth), depthMaterial, 0), "ROV mapping depth");
            lastSession = sensor.Session; lastStamp = stamp;
            nextCapture = now + CaptureInterval;
            queued = true;
        }

        private void ReadFrame(ScriptableRenderContext context, Camera camera)
        {
            if (camera != view || !queued) return;
            queued = false; reading = true;
            var token = lifetime.Token;
            var projection = camera.projectionMatrix;
            var header = Encoding.ASCII.GetBytes(FormattableString.Invariant(
                $"RGBD1 {lastSession} {lastStamp} {projection.m00 * Width / 2} {projection.m11 * Height / 2} {(SystemInfo.graphicsUVStartsAtTop ? 0 : 1)}\n"));
            byte[] pixels = null, metres = null;
            int remaining = 2;
            void Complete(AsyncGPUReadbackRequest result, bool color)
            {
                if (!result.hasError && !token.IsCancellationRequested)
                {
                    if (color) pixels = result.GetData<byte>().ToArray();
                    else metres = result.GetData<byte>().ToArray();
                }
                if (--remaining != 0) return;
                reading = false;
                if (!isActiveAndEnabled) { Release(); return; }
                if (token.IsCancellationRequested) return;
                if (pixels == null || metres == null) { Status = "RGB-D readback failed"; return; }
                send = Send(header, pixels, metres, token);
            }
            AsyncGPUReadback.Request(rgb.rt, 0, result => Complete(result, true));
            AsyncGPUReadback.Request(depth.rt, 0, result => Complete(result, false));
        }

        private static async Task Send(byte[] header, byte[] pixels, byte[] metres, CancellationToken stop)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop);
            timeout.CancelAfter(500);
            using var client = new TcpClient();
            using var abort = timeout.Token.Register(client.Close);
            await client.ConnectAsync("127.0.0.1", Port).ConfigureAwait(false);
            using var stream = client.GetStream();
            await stream.WriteAsync(header, 0, header.Length, timeout.Token).ConfigureAwait(false);
            await stream.WriteAsync(pixels, 0, pixels.Length, timeout.Token).ConfigureAwait(false);
            await stream.WriteAsync(metres, 0, metres.Length, timeout.Token).ConfigureAwait(false);
        }
    }
}
