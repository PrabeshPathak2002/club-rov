using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace ClubROV.Simulation.Editor
{
    public static class RovMapChecks
    {
        public static string Run()
        {
            if(!EditorApplication.isPlaying) throw new InvalidOperationException("Play first");
            var vehicle=UnityEngine.Object.FindAnyObjectByType<RovDynamics>();
            var settings=vehicle.GetComponent<RovTrainingSettings>(); var course=vehicle.GetComponent<RovTrainingCourse>();var maps=vehicle.GetComponent<RovTrainingMaps>();
            bool record=course.RecordResults;course.RecordResults=false; var results=new List<string>();
            void Check(string name,bool pass) {results.Add((pass?"PASS: ":"FAIL: ")+name);}
            void Pose(float x,float z) => course.EvaluatePose(new Vector3(x,-1.6f,z),Quaternion.identity,Vector3.forward,0.02f);
            try
            {
                for(int i=0;i<4;i++) {settings.SelectMap(i);Check("Map "+i+" activates",maps.Current==i);}
                settings.SelectMap(2);Pose(0,4);Check("Free swim has no scored completion",!course.Started && !course.Complete && course.Objective.Contains("FREE SWIM"));
                settings.SelectMap(1);Check("Prequal start 3m behind bar",Vector3.Distance(vehicle.Body.position,new Vector3(0,-1.6f,-8))<0.001f);
                Pose(2,-6);Pose(2,-4);Check("Outside gate rejected",course.GatesPassed==0);
                Pose(0,-6);Pose(0,-4);Check("Outbound gate detected",course.GatesPassed==1);
                Pose(0,-6);Check("Return before circle rejected",!course.Complete);
                Pose(0,3);
                for(int i=0;i<=36;i++) {float a=i*10*Mathf.Deg2Rad;Pose(2*Mathf.Sin(a),5-2*Mathf.Cos(a));}
                Check("Full pillar orbit detected",course.MarkerRounded);
                Pose(0,-4);Pose(0,-6);Check("Return gate completes practice",course.Complete);
                settings.SelectMap(1);Pose(0,-4);Pose(0,3);
                for(int i=0;i<8;i++) {Pose(0,3);Pose(0,7);}
                Check("Teleport jumps cannot accumulate orbit",!course.MarkerRounded);
                settings.SelectMap(0);Check("Original three-gate course restored",course.Objective.Contains("1/3"));
                settings.SelectMap(3);Check("Slalom has five gates",course.Objective.Contains("1/5"));
                var s=settings.Current;Check("Scenario copy retains map",s.Copy().map==3);var key=s.Key();s.map=0;Check("Map changes score identity",s.Key()!=key);
                var root=UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
                Check("Map selector has four choices",root.Q<DropdownField>("training-map").choices.Count==4);
                Check("Map load button present",root.Q<Button>("map-apply")!=null);
            }
            finally {settings.SelectMap(0);course.RecordResults=record;}
            string report=string.Join("\n",results);System.IO.File.WriteAllText("Docs/MapCheckResults.txt",report);
            if(results.Exists(x=>x.StartsWith("FAIL"))) throw new Exception(report);return report;
        }
    }
}
