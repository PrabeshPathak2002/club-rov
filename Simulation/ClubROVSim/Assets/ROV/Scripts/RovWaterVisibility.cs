using UnityEngine;
using UnityEngine.Rendering;

namespace ClubROV.Simulation
{
    [RequireComponent(typeof(Camera))]
    public sealed class RovWaterVisibility : MonoBehaviour
    {
        [SerializeField, Range(1, 20)] private float visibilityMetres = 8;
        private Camera view;
        private bool originalFog;
        private FogMode originalMode;
        private float originalDensity;
        private Color originalColor;
        public float VisibilityMetres => visibilityMetres;
        public void SetVisibility(float metres) => visibilityMetres = Mathf.Clamp(metres, 1, 20);
        private void OnEnable()
        {
            view = GetComponent<Camera>();
            originalFog = RenderSettings.fog; originalMode = RenderSettings.fogMode;
            originalDensity = RenderSettings.fogDensity; originalColor = RenderSettings.fogColor;
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
        }
        private void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == view) ApplyWater();
        }
        public void ApplyWater()
        {
            bool underwater = transform.position.y < 0;
            RenderSettings.fog = underwater;
            RenderSettings.fogMode = FogMode.Exponential;
            // Artistic visibility control: remaining scene contrast is ~2% at this distance.
            RenderSettings.fogDensity = Mathf.Log(50) / visibilityMetres;
            RenderSettings.fogColor = Color.Lerp(new Color(0.035f, 0.17f, 0.16f), new Color(0.055f, 0.3f, 0.36f), visibilityMetres / 20);
            if (view != null) view.backgroundColor = underwater ? RenderSettings.fogColor : new Color(0.34f, 0.55f, 0.68f);
        }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            RenderSettings.fog = originalFog; RenderSettings.fogMode = originalMode;
            RenderSettings.fogDensity = originalDensity; RenderSettings.fogColor = originalColor;
        }
    }
}
