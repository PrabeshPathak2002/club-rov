using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ClubROV.Simulation
{
    [RequireComponent(typeof(RovDynamics), typeof(RovTrainingCourse))]
    public sealed class RovTrainingSettings : MonoBehaviour
    {
        [SerializeField] private RovWaterVisibility water;
        [SerializeField] private RovFpvCameraFeed feed;
        [SerializeField] private RovUnderwaterCamera optics;
        private RovDynamics vehicle;
        private RovTrainingCourse course;
        private RovSitlBridge sitl;
        private RovScenario active = new();
        private readonly List<RovScenario> scenarios = new();
        private RovScenarioFile saved = new();
        private bool persistenceReady = true;
        private string lastController;
        private string SavePath => Path.Combine(Application.persistentDataPath, "training-scenarios.json");
        public IReadOnlyList<RovScenario> Scenarios => scenarios;
        public RovScenario Current => active.Copy();
        public string Notice { get; private set; } = "Apply a preset before starting a scored run.";
        public void Configure(RovWaterVisibility visibility, RovFpvCameraFeed cameraFeed, RovUnderwaterCamera cameraOptics)
        { water = visibility; feed = cameraFeed; optics = cameraOptics; }
        private void Awake()
        {
            vehicle = GetComponent<RovDynamics>(); course = GetComponent<RovTrainingCourse>(); sitl = GetComponent<RovSitlBridge>();
            scenarios.Add(new RovScenario());
            scenarios.Add(new RovScenario { name="Cross current", current=new Vector3(0.35f,0,0), visibility=6 });
            scenarios.Add(new RovScenario { name="Murky water", visibility=3, particles=0.8f, videoDelayMs=200 });
            scenarios.Add(new RovScenario { name="H2 failure", failedThruster=1, buoyancyPercent=2 });
            try
            {
                saved = RovTrainingData.Read<RovScenarioFile>(SavePath) ?? new RovScenarioFile();
                if (saved.version != 1 || saved.scenarios == null || saved.scenarios.Count > 12) throw new IOException("Unsupported scenario file.");
                foreach (var item in saved.scenarios) { item.Validate(); scenarios.Add(item); }
            }
            catch (Exception error) { persistenceReady=false; Notice="Scenario save unavailable: " + error.Message; Debug.LogWarning(Notice); }
        }
        private void Start() { ApplyConditions(); UpdateKey(); }
        private void Update()
        {
            string controller = sitl != null && sitl.Active ? "SITL " + sitl.ControlMode : "LOCAL";
            if (controller == lastController) return;
            lastController=controller; UpdateKey();
        }
        private void UpdateKey() => course.SetRunConditions(active.Key() + ":" + lastController, active.name);
        public void Change(RovScenario values)
        {
            values.Validate(); active=values.Copy(); ApplyConditions(); UpdateKey();
            Notice=course.Started ? "Conditions changed · this run is practice only." : "Settings ready · apply a preset to reset the start.";
        }
        private void ApplyConditions()
        {
            vehicle.SetCurrent(active.current); vehicle.SetBuoyancyBias(active.buoyancyPercent / 100);
            for (int i=0;i<6;i++) vehicle.SetThrusterHealth(i, i==active.failedThruster ? active.thrusterPercent/100 : 1);
            water.SetVisibility(active.visibility); feed.SetDelay(active.videoDelayMs);
            optics.ConfigureEffects(active.particles, active.light);
        }
        public void Apply(int index)
        {
            if (vehicle.ExternalPhysics) { Notice="Disconnect SITL and restart both ends before loading a start pose."; return; }
            active=scenarios[index].Copy(); active.Validate(); ApplyConditions();
            lastController="LOCAL"; UpdateKey(); vehicle.SetStartPose(active.startPosition,Quaternion.Euler(0,active.startHeading,0));
            Notice="Loaded " + active.name + " · repeatable start and conditions.";
        }
        public void Save(string name)
        {
            if (!persistenceReady) throw new IOException("Existing scenario file needs repair; it has been preserved.");
            var item=Current; item.name=name.Trim(); item.startPosition=vehicle.Body.position; item.startHeading=vehicle.Heading; item.Validate();
            for(int i=0;i<4;i++) if(scenarios[i].name==item.name) throw new IOException("Choose a custom name different from a built-in preset.");
            int existing=saved.scenarios.FindIndex(s=>s.name==item.name);
            if (existing<0 && saved.scenarios.Count>=12) throw new IOException("Twelve custom scenarios are already saved.");
            var next=new RovScenarioFile { scenarios=new List<RovScenario>(saved.scenarios) };
            if(existing>=0) next.scenarios[existing]=item; else next.scenarios.Add(item);
            RovTrainingData.Write(SavePath,next); saved=next;
            scenarios.RemoveRange(4,scenarios.Count-4); scenarios.AddRange(saved.scenarios);
            Notice="Saved " + item.name + " with the current vehicle pose. Select it and Apply to begin.";
        }
    }
}
