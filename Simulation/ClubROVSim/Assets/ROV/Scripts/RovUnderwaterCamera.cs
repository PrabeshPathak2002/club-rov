using UnityEngine;

namespace ClubROV.Simulation
{
    [RequireComponent(typeof(RovFpvCameraFeed))]
    public sealed class RovUnderwaterCamera : MonoBehaviour
    {
        [SerializeField] private Shader particleShader;
        public void ConfigureShader(Shader shader) => particleShader=shader;
        private RovFpvCameraFeed feed;
        private ParticleSystem particles;
        private Light lamp;
        private Material material;
        private Texture2D dot;
        private GameObject effects;
        private float density=0.35f, brightness=0.65f;
        public float Density => density;
        public float Brightness => brightness;
        public void ConfigureEffects(float particulate, float lighting)
        { density=Mathf.Clamp01(particulate); brightness=Mathf.Clamp01(lighting); }
        private void OnEnable()
        {
            feed=GetComponent<RovFpvCameraFeed>();
            effects=new GameObject("Underwater camera effects"); effects.transform.SetParent(transform,false);
            lamp=effects.AddComponent<Light>(); lamp.type=LightType.Spot; lamp.range=9; lamp.spotAngle=65;
            lamp.color=new Color(0.7f,0.9f,1); lamp.shadows=LightShadows.None;
            var cloud=new GameObject("Suspended particles"); cloud.transform.SetParent(effects.transform,false);
            cloud.transform.localPosition=new Vector3(0,0,4);
            particles=cloud.AddComponent<ParticleSystem>(); particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.useAutoRandomSeed=false; particles.randomSeed=42;
            var main=particles.main; main.maxParticles=400; main.startLifetime=10; main.startSpeed=0.015f;
            main.startSize=new ParticleSystem.MinMaxCurve(0.008f,0.025f); main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.startColor=new Color(0.65f,0.85f,0.9f,0.15f);
            var shape=particles.shape; shape.shapeType=ParticleSystemShapeType.Box; shape.scale=new Vector3(10,5,9);
            var shader=particleShader;
            if(shader!=null)
            {
                material=new Material(shader); material.SetFloat("_Surface",1); material.SetFloat("_Blend",0);
                dot=new Texture2D(16,16,TextureFormat.RGBA32,false);
                for(int y=0;y<16;y++) for(int x=0;x<16;x++)
                {
                    float alpha=Mathf.Clamp01(1-new Vector2((x-7.5f)/7.5f,(y-7.5f)/7.5f).sqrMagnitude);
                    dot.SetPixel(x,y,new Color(1,1,1,alpha*alpha));
                }
                dot.Apply(); material.SetTexture("_BaseMap",dot);
                material.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite",0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue=3000;
                var renderer=cloud.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=material;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows=false;
            }
            else cloud.GetComponent<ParticleSystemRenderer>().enabled=false;
        }
        private void LateUpdate()
        {
            bool show=feed.Active && transform.position.y<0;
            lamp.enabled=show && brightness>0; lamp.intensity=brightness*12;
            var emission=particles.emission; emission.rateOverTime=40*density;
            if(show && density>0) { if(!particles.isPlaying) particles.Play(); }
            else if(particles.isPlaying) particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        private void OnDisable()
        {
            if(effects!=null) Destroy(effects);
            if(material!=null) Destroy(material);
            if(dot!=null) Destroy(dot);
        }
    }
}
