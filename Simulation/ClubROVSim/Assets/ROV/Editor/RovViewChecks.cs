using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClubROV.Simulation.Editor
{
    public static class RovViewChecks
    {
        public static string Run()
        {
            if(!EditorApplication.isPlaying) throw new InvalidOperationException("Run in Play Mode.");
            var rig=UnityEngine.Object.FindAnyObjectByType<RovCameraRig>();
            var hud=UnityEngine.Object.FindAnyObjectByType<RovHud>();
            var root=hud.GetComponent<UIDocument>().rootVisualElement;
            var camera=rig.GetComponent<Camera>(); var feed=rig.GetComponent<RovFpvCameraFeed>();
            var original=rig.Mode; var results=new List<string>();
            void Check(string name,bool pass) { if(!pass)throw new Exception(name); results.Add("PASS: "+name); }
            void Click(Button b) => typeof(Clickable).GetMethod("Invoke",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(b.clickable,new object[]{null});
            try
            {
                rig.SetView(RovCameraRig.ViewMode.Chase); rig.CycleView();
                Check("Chase cycles to FPV",rig.PilotView && feed.Active);
                rig.CycleView(); rig.SendMessage("LateUpdate");
                Check("FPV cycles to top-down",rig.Mode==RovCameraRig.ViewMode.TopDown);
                Check("Top-down is orthographic with native aspect",camera.orthographic && camera.orthographicSize==5 && Mathf.Abs(camera.aspect-(float)Screen.width/Screen.height)<0.01f);
                Check("Top-down disables FPV effects",!feed.Active);
                Check("Top-down looks down with north up",Vector3.Dot(rig.transform.forward,Vector3.down)>0.999f && Vector3.Dot(rig.transform.up,Vector3.forward)>0.999f);
                rig.CycleView();rig.SendMessage("LateUpdate");
                Check("Top-down cycles back to perspective chase",rig.Mode==RovCameraRig.ViewMode.Chase && !camera.orthographic);
                foreach(var id in new[]{"control","course","vehicle","debug"})
                {
                    var button=root.Q<Button>("open-"+id); var content=root.Q("panel-content-"+id);
                    Click(button);
                    Check(id+" drawer opens",content.parent.style.display.value==DisplayStyle.Flex);
                    foreach(var other in new[]{"control","course","vehicle","debug"})
                        if(other!=id) Check(other+" stays closed while "+id+" is open",root.Q("panel-content-"+other).parent.style.display.value==DisplayStyle.None);
                    Click(button);
                    Check(id+" drawer toggles closed",content.parent.style.display.value==DisplayStyle.None);
                }
                Click(root.Q<Button>("open-control")); Click(root.Q<Button>("close-panel"));
                Check("Close button closes the drawer",root.Q("hud-drawer").style.display.value==DisplayStyle.None);
                Click(root.Q<Button>("hide-hud"));
                Check("Hide-all removes HUD",root.Q("root").ClassListContains("hud-hidden"));
                var panels=(RovHudPanels)typeof(RovHud).GetField("panels",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(hud);
                Click(root.Q<Button>("show-hud"));
                Check("HUD restores after hide-all",!root.Q("root").ClassListContains("hud-hidden"));
                hud.enabled=false;hud.enabled=true;
                Check("HUD re-enable does not duplicate panel controls",root.Query<Button>("open-control").ToList().Count==1);
                Check("HUD re-enable does not duplicate DEBUG",root.Query<Button>("open-debug").ToList().Count==1 && root.Query<VisualElement>("debug-panel").ToList().Count==1);
                Check("Panels start closed after re-enable",root.Q("hud-drawer").style.display.value==DisplayStyle.None);
            }
            finally {rig.SetView(original);rig.SendMessage("LateUpdate");}
            var report=string.Join("\n",results);
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath,"../Docs/ViewCheckResults.txt"),report);
            return report;
        }
    }
}
