using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClubROV.Simulation.Editor
{
    public static class RovFlightOverlayChecks
    {
        public static string Run()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Run in Play Mode.");
            var results=new List<string>();
            void Check(string name,bool pass){if(!pass)throw new Exception(name);results.Add("PASS: "+name);}
            var level=RovFlightOverlay.Attitude(Quaternion.identity);
            Check("Level attitude is zero",level.sqrMagnitude<0.001f);
            foreach(float yaw in new[]{0f,90f,180f,270f,359f,1f})
                Check("Heading "+yaw,Mathf.Abs(Mathf.DeltaAngle(RovFlightOverlay.Attitude(Quaternion.Euler(0,yaw,0)).z,yaw))<0.01f);
            var bank=RovFlightOverlay.Attitude(Quaternion.Euler(-12,35,-20));
            Check("Nose up pitch and right bank signs",Mathf.Abs(bank.x-12)<0.01f && Mathf.Abs(bank.y-20)<0.01f);
            var inverted=RovFlightOverlay.Attitude(Quaternion.Euler(0,0,180));
            Check("Inverted roll is finite",float.IsFinite(inverted.y) && Mathf.Abs(inverted.y)>179);
            var hud=UnityEngine.Object.FindAnyObjectByType<RovHud>();
            var rig=UnityEngine.Object.FindAnyObjectByType<RovCameraRig>();var original=rig.Mode;
            var root=hud.GetComponent<UIDocument>().rootVisualElement;
            var overlay=root.Q<RovFlightOverlay>("flight-osd");
            try
            {
                rig.SetView(RovCameraRig.ViewMode.Fpv);overlay.Tick();
                Check("FPV instruments visible by default",overlay.Visible);
                root.Q("root").AddToClassList("hud-hidden");overlay.Tick();
                Check("Panel hiding preserves flight instruments",overlay.Visible && overlay.style.display.value==DisplayStyle.Flex);
                overlay.Toggle();overlay.Tick();Check("Independent OSD toggle",!overlay.Visible);
                overlay.Toggle();rig.SetView(RovCameraRig.ViewMode.TopDown);overlay.Tick();Check("Top-down has no flight overlay",!overlay.Visible);
                rig.SetView(RovCameraRig.ViewMode.Chase);overlay.Tick();Check("Chase has no flight overlay",!overlay.Visible);
                Check("Overlay does not intercept pilot clicks",overlay.pickingMode==PickingMode.Ignore);
            }
            finally {root.Q("root").RemoveFromClassList("hud-hidden");rig.SetView(original);overlay.Tick();}
            hud.enabled=false;hud.enabled=true;
            Check("Re-enable creates one overlay and one toggle",root.Query<RovFlightOverlay>().ToList().Count==1 && root.Query<Button>("osd-toggle").ToList().Count==1);
            var report=string.Join("\n",results);
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath,"../Docs/FlightOverlayCheckResults.txt"),report);
            return report;
        }
    }
}
