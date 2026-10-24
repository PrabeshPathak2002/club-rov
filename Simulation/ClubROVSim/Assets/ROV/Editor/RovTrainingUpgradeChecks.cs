using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClubROV.Simulation.Editor
{
    public static class RovTrainingUpgradeChecks
    {
        private static readonly List<string> passed=new();
        private static RovTrainingSettings settings;
        private static RovDynamics vehicle;
        private static RovTrainingCourse course;
        private static RovPilotInput pilot;
        private static RovSitlBridge bridge;
        private static RovCameraRig rig;
        private static RovFpvCameraFeed feed;
        private static RovScenario original;
        private static Vector3 before;
        private static bool inputEnabled,recordResults,view;
        private static int stage;
        private static double deadline;
        private static void Check(bool condition,string label)
        { if(!condition) throw new Exception(label); passed.Add(label); }
        public static string Start()
        {
            if(!EditorApplication.isPlaying) throw new Exception("Enter Play Mode first.");
            settings=UnityEngine.Object.FindAnyObjectByType<RovTrainingSettings>();
            vehicle=settings.GetComponent<RovDynamics>(); course=settings.GetComponent<RovTrainingCourse>();
            pilot=settings.GetComponent<RovPilotInput>(); bridge=settings.GetComponent<RovSitlBridge>();
            rig=UnityEngine.Object.FindAnyObjectByType<RovCameraRig>(); feed=rig.GetComponent<RovFpvCameraFeed>();
            if(bridge.Active) throw new Exception("Disconnect SITL before isolated checks.");
            original=settings.Current; inputEnabled=pilot.enabled; recordResults=course.RecordResults; view=rig.PilotView;
            pilot.enabled=false; course.RecordResults=false; passed.Clear();
            try
            {
                var a=new RovScenario(); a.Validate(); string key=a.Key(); a.visibility=3;
                Check(a.Key()!=key,"scenario key separates different conditions");
                bool rejected=false; a.startPosition.x=99; try{a.Validate();}catch(ArgumentException){rejected=true;}
                Check(rejected,"out-of-pool start rejected");
                rejected=false; a=new RovScenario { current=new Vector3(float.NaN,0,0) };
                try{a.Validate();}catch(ArgumentException){rejected=true;}
                Check(rejected,"non-finite scenario rejected");
                Check(RovTrainingData.Score(10,1,0.1f,10)==900,"score accounts for time, collisions, depth and heading");
                var runs=RovTrainingData.AppendRun(new RovRunFile(),new RovRunResult {scenarioKey="A",score=900});
                for(int i=0;i<101;i++) runs=RovTrainingData.AppendRun(runs,new RovRunResult {scenarioKey="B",score=100});
                Check(runs.runs.Count==100 && runs.bestRuns.Find(r=>r.scenarioKey=="A").score==900,"personal best survives recent-run history rotation");
                runs=RovTrainingData.AppendRun(runs,new RovRunResult {scenarioKey="A",score=800});
                Check(runs.bestRuns.Find(r=>r.scenarioKey=="A").score==900,"lower scoring run cannot replace a personal best");
                string path=Path.GetFullPath("Library/TrainingUpgradeCheck.json");
                try
                {
                    var data=new RovScenarioFile(); data.scenarios.Add(new RovScenario {name="Check",current=new Vector3(0.3f,0,0)});
                    RovTrainingData.Write(path,data);
                    var loaded=RovTrainingData.Read<RovScenarioFile>(path);
                    Check(loaded.version==1 && loaded.scenarios[0].name=="Check" && loaded.scenarios[0].current.x==0.3f,"scenario round-trip preserves conditions and version");
                    data.scenarios[0].name="Updated"; RovTrainingData.Write(path,data);
                    Check(RovTrainingData.Read<RovScenarioFile>(path+".bak").scenarios[0].name=="Check","atomic save retains prior backup");
                    File.WriteAllText(path,"not json"); rejected=false;
                    try{RovTrainingData.Read<RovScenarioFile>(path);}catch(ArgumentException){rejected=true;}
                    Check(rejected,"corrupt save is reported");
                }
                finally {foreach(string file in new[]{path,path+".bak",path+".tmp"}) if(File.Exists(file)) File.Delete(file);}
                settings.Apply(1); before=vehicle.Body.position; stage=0; deadline=EditorApplication.timeSinceStartup+1.2;
                EditorApplication.update+=Tick;
                return "Training upgrade checks running (physics, UI, FPV latency and telemetry).";
            }
            catch(Exception error) {Finish(error.Message);return "Checks failed: "+error.Message;}
        }
        private static void Status(bool armed,int sysid=42)
        {
            using var client=new UdpClient();
            byte[] bytes=System.Text.Encoding.UTF8.GetBytes("{\"sysid\":"+sysid+",\"armed\":"+(armed?"true":"false")+",\"mode\":2}");
            client.Send(bytes,bytes.Length,new IPEndPoint(IPAddress.Loopback,9004));
        }
        private static void Tick()
        {
            if(EditorApplication.timeSinceStartup<deadline) return;
            try
            {
                if(stage==0)
                {
                    Check(vehicle.Body.position.x>before.x+0.01f,"cross current produces drift with neutral motors");
                    settings.Apply(0); var s=settings.Current; s.buoyancyPercent=10; settings.Change(s);
                    before=vehicle.Body.position; deadline=EditorApplication.timeSinceStartup+1.2; stage++;
                }
                else if(stage==1)
                {
                    Check(vehicle.Body.position.y>before.y+0.03f,"positive buoyancy bias raises the vehicle");
                    settings.Apply(3); vehicle.SetPilotCommand(Vector3.forward,0);
                    deadline=EditorApplication.timeSinceStartup+0.5; stage++;
                }
                else if(stage==2)
                {
                    Check(vehicle.Output(1)==0 && Mathf.Abs(vehicle.Output(0))>0.1f,"selected failed thruster loses force while others respond");
                    vehicle.StopThrusters(); settings.Apply(0);
                    course.EvaluatePose(vehicle.Body.position,vehicle.Body.rotation,Vector3.forward,0.1f);
                    var s=settings.Current; s.visibility=5; settings.Change(s);
                    Check(!course.Qualified,"changing conditions invalidates a started scored run");
                    var root=UnityEngine.Object.FindAnyObjectByType<RovHud>().GetComponent<UIDocument>().rootVisualElement;
                    root.Q<Slider>("current-east").value=0.2f;
                    Check(Mathf.Abs(vehicle.CurrentVelocity.x-0.2f)<0.001f,"HUD current slider reaches physics");
                    settings.Apply(2); Check(feed.DelayMilliseconds==200,"murky preset configures camera delay");
                    if(!rig.PilotView) rig.ToggleView(); deadline=EditorApplication.timeSinceStartup+1.2; stage++;
                }
                else if(stage==3)
                {
                    Check(feed.BufferAllocated && feed.StoredFrames==feed.DelayFrames+1 && feed.PresentedAgeFrames==feed.DelayFrames,"FPV presents delayed history after warmup");
                    Check(rig.GetComponentInChildren<Light>().enabled,"underwater FPV lamp enabled");
                    Check(rig.GetComponentInChildren<ParticleSystem>().isPlaying,"suspended particles run in underwater FPV");
                    rig.ToggleView(); deadline=EditorApplication.timeSinceStartup+0.2; stage++;
                }
                else if(stage==4)
                {
                    Check(!feed.BufferAllocated && feed.StoredFrames==0,"chase view releases delayed frame buffers");
                    Check(!rig.GetComponentInChildren<Light>().enabled && !rig.GetComponentInChildren<ParticleSystem>().isPlaying,"chase view disables camera effects");
                    settings.Apply(0); bridge.Connect(); Status(true,7);
                    deadline=EditorApplication.timeSinceStartup+0.2; stage++;
                }
                else if(stage==5)
                {
                    Check(!bridge.TelemetryFresh,"telemetry from another vehicle ignored"); Status(true);
                    deadline=EditorApplication.timeSinceStartup+0.2; stage++;
                }
                else if(stage==6)
                {
                    Check(bridge.Armed && bridge.ControlMode=="Depth hold","arming badge uses actual simulator heartbeat"); Status(false);
                    deadline=EditorApplication.timeSinceStartup+0.2; stage++;
                }
                else if(stage==7)
                {
                    Check(!bridge.Armed && bridge.ControlState.StartsWith("DISARMED"),"disarmed state is explicit");
                    deadline=EditorApplication.timeSinceStartup+3.2; stage++;
                }
                else if(stage==8) {Check(!bridge.TelemetryFresh && bridge.ControlState=="ARM STATE UNKNOWN","stale heartbeat does not claim disarmed or armed");Finish(null);}
            }
            catch(Exception error) {Finish(error.Message);}
        }
        private static void Finish(string error)
        {
            EditorApplication.update-=Tick; bridge?.Disconnect();
            if(settings!=null) {settings.Apply(0);settings.Change(original);vehicle.SetStartPose(original.startPosition,Quaternion.Euler(0,original.startHeading,0));}
            if(course!=null) course.RecordResults=recordResults;
            if(pilot!=null) pilot.enabled=inputEnabled;
            if(rig!=null && rig.PilotView!=view) rig.ToggleView();
            string result=(error==null?"PASS: "+passed.Count+" training upgrade checks":"FAIL: "+error)+"\n"+string.Join("\n",passed);
            File.WriteAllText("TrainingUpgradeCheckResults.txt",result);
            if(error==null) Debug.Log(result); else Debug.LogError(result);
        }
    }
}
