using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClubROV.Simulation
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class RovTrainingPanel : MonoBehaviour
    {
        [SerializeField] private RovTrainingSettings settings;
        private VisualElement root;
        private readonly List<Action> unbind=new();
        private DropdownField scenario, failed;
        private Label notice;
        private Button apply;
        private RovPilotInput pilot;
        private string error;
        public void Configure(RovTrainingSettings training) => settings=training;
        private bool started;
        private void OnEnable() { if(started) Bind(); }
        private void Start() { started=true; Bind(); }
        private void Bind()
        {
            if(settings==null) return;
            pilot=settings.GetComponent<RovPilotInput>(); root=GetComponent<UIDocument>().rootVisualElement;
            scenario=root.Q<DropdownField>("scenario"); failed=root.Q<DropdownField>("failed-thruster");
            notice=root.Q<Label>("scenario-notice"); apply=root.Q<Button>("scenario-apply");
            RefreshNames();
            var map=root.Q<DropdownField>("training-map");
            map.choices=new List<string>(RovTrainingMaps.Names); map.SetValueWithoutNotify(map.choices[settings.Current.map]);
            var loadMap=root.Q<Button>("map-apply");
            Action switchMap=()=>Try(()=>{settings.SelectMap(map.index);Sync();});
            loadMap.clicked+=switchMap; unbind.Add(()=>loadMap.clicked-=switchMap);
            failed.choices=new List<string>{"None","H1 front port","H2 front starboard","H3 rear port","H4 rear starboard","V1 port","V2 starboard"};
            EventCallback<ChangeEvent<string>> onFailure=e=>Change(s=>s.failedThruster=failed.index-1);
            failed.RegisterValueChangedCallback(onFailure); unbind.Add(()=>failed.UnregisterValueChangedCallback(onFailure));
            Slider("current-east",s=>s.current.x,(s,v)=>s.current=Vector3.ClampMagnitude(new Vector3(v,s.current.y,s.current.z),1));
            Slider("current-north",s=>s.current.z,(s,v)=>s.current=Vector3.ClampMagnitude(new Vector3(s.current.x,s.current.y,v),1));
            Slider("buoyancy",s=>s.buoyancyPercent,(s,v)=>s.buoyancyPercent=v);
            Slider("thruster-health",s=>s.thrusterPercent,(s,v)=>s.thrusterPercent=v);
            Slider("delay",s=>s.videoDelayMs,(s,v)=>s.videoDelayMs=v);
            Slider("particles",s=>s.particles,(s,v)=>s.particles=v);
            Slider("light",s=>s.light,(s,v)=>s.light=v);
            Action load=()=>Try(()=>{settings.Apply(scenario.index);Sync();});
            apply.clicked+=load; unbind.Add(()=>apply.clicked-=load);
            var save=root.Q<Button>("scenario-save");
            Action store=()=>Try(()=>{settings.Save(root.Q<TextField>("scenario-name").value);RefreshNames();});
            save.clicked+=store; unbind.Add(()=>save.clicked-=store);
            Sync();
            root.focusable=true;
            root.schedule.Execute(()=>root.Focus());
        }
        private void RefreshNames()
        {
            var choices=new List<string>(); foreach(var item in settings.Scenarios) choices.Add(item.name);
            scenario.choices=choices; scenario.SetValueWithoutNotify(choices[0]);
        }
        private void Slider(string name,Func<RovScenario,float> get,Action<RovScenario,float> set)
        {
            var control=root.Q<Slider>(name);
            EventCallback<ChangeEvent<float>> callback=e=>Change(s=>set(s,e.newValue));
            control.RegisterValueChangedCallback(callback); unbind.Add(()=>control.UnregisterValueChangedCallback(callback));
        }
        private void Change(Action<RovScenario> edit) => Try(()=>{var s=settings.Current;edit(s);settings.Change(s);Sync();});
        private void Try(Action action)
        {
            try {action();error=null;} catch(Exception exception) {error=exception.Message;}
        }
        private void Sync()
        {
            var s=settings.Current;
            root.Q<DropdownField>("training-map").SetValueWithoutNotify(RovTrainingMaps.Names[s.map]);
            root.Q<Slider>("current-east").SetValueWithoutNotify(s.current.x);
            root.Q<Slider>("current-north").SetValueWithoutNotify(s.current.z);
            root.Q<Slider>("buoyancy").SetValueWithoutNotify(s.buoyancyPercent);
            root.Q<Slider>("thruster-health").SetValueWithoutNotify(s.thrusterPercent);
            root.Q<Slider>("delay").SetValueWithoutNotify(s.videoDelayMs);
            root.Q<Slider>("particles").SetValueWithoutNotify(s.particles);
            root.Q<Slider>("light").SetValueWithoutNotify(s.light);
            root.Q<Slider>("visibility").SetValueWithoutNotify(s.visibility);
            failed.SetValueWithoutNotify(failed.choices[s.failedThruster+1]);
        }
        private void Update()
        {
            if(root==null) return;
            var focus=root.panel?.focusController?.focusedElement as VisualElement;
            pilot.SuppressInput=focus is TextField || focus?.GetFirstAncestorOfType<TextField>()!=null;
            apply.SetEnabled(!settings.GetComponent<RovDynamics>().ExternalPhysics);
            root.Q<Button>("map-apply").SetEnabled(!settings.GetComponent<RovDynamics>().ExternalPhysics);
            root.Q<Label>("map-description").text=RovTrainingMaps.Descriptions[settings.Current.map];
            notice.text=error ?? settings.Notice;
        }
        private void OnDisable()
        {
            foreach(var action in unbind) action(); unbind.Clear();
            if(pilot!=null) pilot.SuppressInput=false;
        }
    }
}
