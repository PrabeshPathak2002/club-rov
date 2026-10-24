using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace ClubROV.Simulation
{
    // Own a runtime pipeline copy: graphics choices never dirty the project asset.
    public sealed class RovGraphicsSettings : IDisposable
    {
        private readonly RenderPipelineAsset original;
        private readonly UniversalRenderPipelineAsset pipeline;
        private readonly DropdownField quality, cap;
        private readonly Label performance;
        private readonly int oldCap, oldVsync;
        private float elapsed;
        private int frames;
        public RovGraphicsSettings(VisualElement root)
        {
            oldCap=Application.targetFrameRate; oldVsync=QualitySettings.vSyncCount;
            original=QualitySettings.renderPipeline;
            var source=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(source!=null) { pipeline=UnityEngine.Object.Instantiate(source); QualitySettings.renderPipeline=pipeline; }
            quality=root.Q<DropdownField>("graphics-quality"); cap=root.Q<DropdownField>("graphics-cap");
            performance=root.Q<Label>("performance");
            quality.choices=new List<string>{"Low","Medium","High"};
            cap.choices=new List<string>{"30","60","120"};
            quality.SetValueWithoutNotify(quality.choices[Mathf.Clamp(PlayerPrefs.GetInt("graphics.quality",1),0,2)]);
            cap.SetValueWithoutNotify(cap.choices[Mathf.Clamp(PlayerPrefs.GetInt("graphics.cap",1),0,2)]);
            quality.RegisterValueChangedCallback(Changed); cap.RegisterValueChangedCallback(Changed);
            Apply();
        }
        private void Changed(ChangeEvent<string> evt) { Apply(); PlayerPrefs.SetInt("graphics.quality",quality.index); PlayerPrefs.SetInt("graphics.cap",cap.index); PlayerPrefs.Save(); }
        private void Apply()
        {
            int level=quality.index;
            if(pipeline!=null)
            {
                pipeline.renderScale=level==0?0.65f:level==1?0.85f:1f;
                pipeline.shadowDistance=level==0?0:level==1?15:30;
                pipeline.shadowCascadeCount=level==2?4:1;
                pipeline.supportsHDR=level==2;
            }
            QualitySettings.vSyncCount=0; Application.targetFrameRate=int.Parse(cap.value);
        }
        public void Tick()
        {
            elapsed+=Time.unscaledDeltaTime; frames++;
            if(elapsed<0.5f) return;
            if(performance!=null) performance.text=$"{frames/elapsed:0} FPS · {1000*elapsed/frames:0.0} ms/frame · {quality.value}";
            elapsed=0; frames=0;
        }
        public void Dispose()
        {
            quality.UnregisterValueChangedCallback(Changed); cap.UnregisterValueChangedCallback(Changed);
            QualitySettings.renderPipeline=original;
            if(pipeline!=null) UnityEngine.Object.Destroy(pipeline);
            Application.targetFrameRate=oldCap; QualitySettings.vSyncCount=oldVsync;
        }
    }
}
