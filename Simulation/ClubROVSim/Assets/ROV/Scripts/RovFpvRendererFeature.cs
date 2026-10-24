using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace ClubROV.Simulation
{
    public sealed class RovFpvRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader feedShader;
        private Material material;
        private FeedPass pass;
        public void Configure(Shader shader) { feedShader = shader; Create(); }
        public override void Create()
        {
            pass?.Detach();
            CoreUtils.Destroy(material);
            pass = null; material = null;
            if (feedShader == null) return;
            material = CoreUtils.CreateEngineMaterial(feedShader);
            pass = new FeedPass(material) { renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing };
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (pass == null || renderingData.cameraData.cameraType != CameraType.Game) return;
            var feed = renderingData.cameraData.camera.GetComponent<RovFpvCameraFeed>();
            if (feed == null) return;
            if (!feed.Active) { pass.Release(); feed.BufferAllocated = false; return; }
            pass.Setup(feed);
            renderer.EnqueuePass(pass);
        }
        protected override void Dispose(bool disposing) { pass?.Detach(); CoreUtils.Destroy(material); }

        private sealed class FeedPass : ScriptableRenderPass
        {
            private static readonly int FeedParams = Shader.PropertyToID("_FeedParams");
            private static readonly int DisplayParams = Shader.PropertyToID("_DisplayParams");
            private readonly Material material;
            private RovFpvCameraFeed feed;
            private RTHandle[] frames;
            private int writeIndex, available;
            private float nextCapture = float.NegativeInfinity;
            public FeedPass(Material effect) { material = effect; requiresIntermediateTexture = true; }
            public void Setup(RovFpvCameraFeed cameraFeed)
            {
                if(feed==cameraFeed) return;
                Detach(); feed=cameraFeed; feed.Stopped+=Release;
            }
            public void Detach()
            {
                if(feed!=null) feed.Stopped-=Release;
                Release(); feed=null;
            }
            public void Release()
            {
                if(frames!=null) foreach(var frame in frames) frame?.Release();
                frames=null; writeIndex=available=0;
                if (feed != null) { feed.BufferAllocated = false; feed.StoredFrames=feed.PresentedAgeFrames=0; }
                nextCapture = float.NegativeInfinity;
            }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                if (resources.isActiveTargetBackBuffer || !resources.activeColorTexture.IsValid()) return;
                var source = resources.activeColorTexture;
                var descriptor = cameraData.cameraTargetDescriptor;
                descriptor.width = feed.Width; descriptor.height = feed.Height;
                descriptor.depthBufferBits = 0; descriptor.msaaSamples = 1;
                descriptor.useMipMap = false; descriptor.autoGenerateMips = false;
                int capacity=feed.DelayFrames+1;
                if(frames==null || frames.Length!=capacity) { Release(); frames=new RTHandle[capacity]; }
                bool allocated=false;
                for(int i=0;i<frames.Length;i++) allocated |= RenderingUtils.ReAllocateHandleIfNeeded(ref frames[i], descriptor,
                    FilterMode.Bilinear, TextureWrapMode.Clamp, name: "ROV FPV frame " + i);
                if(allocated) { writeIndex=available=0; nextCapture=float.NegativeInfinity; }
                feed.BufferAllocated = true;
                material.SetVector(DisplayParams, new Vector4((float)feed.Width / feed.Height,
                    (float)cameraData.cameraTargetDescriptor.width / cameraData.cameraTargetDescriptor.height, feed.Softness, 0));
                if (allocated || Time.unscaledTime >= nextCapture)
                {
                    nextCapture = Time.unscaledTime + 1f / feed.FramesPerSecond;
                    material.SetVector(FeedParams, new Vector4(feed.Width, feed.Height, feed.CapturedFrames, feed.SensorNoise));
                    var capture=graph.ImportTexture(frames[writeIndex]);
                    graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, capture, material, 0), "ROV sensor and stream sample");
                    writeIndex=(writeIndex+1)%capacity; available=Mathf.Min(capacity,available+1);
                    feed.RecordCapture();
                }
                int displayIndex=(writeIndex-1-Mathf.Min(feed.DelayFrames,available-1)+capacity)%capacity;
                feed.StoredFrames=available; feed.PresentedAgeFrames=Mathf.Min(feed.DelayFrames,available-1);
                var history=graph.ImportTexture(frames[displayIndex]);
                var outputDescription = graph.GetTextureDesc(source);
                outputDescription.name = "ROV FPV display"; outputDescription.clearBuffer = false;
                var output = graph.CreateTexture(outputDescription);
                graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(history, output, material, 1), "ROV FPV letterbox display");
                resources.cameraColor = output;
            }
        }
    }
}
