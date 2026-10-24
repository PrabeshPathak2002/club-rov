using UnityEngine;

namespace ClubROV.Simulation
{
    [RequireComponent(typeof(RovCameraRig))]
    public sealed class RovFpvCameraFeed : MonoBehaviour
    {
        [SerializeField, Range(160, 1920)] private int width = 640;
        [SerializeField, Range(120, 1080)] private int height = 480;
        [SerializeField, Range(5, 60)] private int framesPerSecond = 30;
        [SerializeField, Range(20, 100)] private float verticalFieldOfView = 48.8f;
        [SerializeField, Range(0, 0.08f)] private float sensorNoise = 0.0025f;
        [SerializeField, Range(0, 1)] private float softness = 0.45f;
        private RovCameraRig rig;
        private float delayMs;
        public float DelayMilliseconds => delayMs;
        public int DelayFrames => Mathf.CeilToInt(delayMs * framesPerSecond / 1000);
        public int StoredFrames { get; internal set; }
        public int PresentedAgeFrames { get; internal set; }
        public void SetDelay(float milliseconds) => delayMs=Mathf.Clamp(milliseconds,0,500);
        public bool Active => enabled && rig != null && rig.PilotView;
        public int Width => width;
        public int Height => height;
        public int FramesPerSecond => framesPerSecond;
        public float VerticalFieldOfView => verticalFieldOfView;
        public float SensorNoise => sensorNoise;
        public float Softness => softness;
        public int CapturedFrames { get; private set; }
        public bool BufferAllocated { get; internal set; }
        public event System.Action Stopped;
        private void Awake() => rig = GetComponent<RovCameraRig>();
        public void RecordCapture() => CapturedFrames++;
        private void OnDisable() => Stopped?.Invoke();
    }
}
