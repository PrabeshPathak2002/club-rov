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
                foreach(var id in new[]{"control","course","vehicle"})
                {
                    var button=root.Q<Button>("minimize-"+id); var content=root.Q("panel-content-"+id);
                    Click(button);
                    Check(id+" panel minimizes",content.style.display.value==DisplayStyle.None);
                    Click(button);
                    Check(id+" panel restores",content.style.display.value==DisplayStyle.Flex);
                }
                Click(root.Q<Button>("hide-hud"));
                Check("Hide-all removes HUD",root.Q("root").ClassListContains("hud-hidden"));
                var panels=(RovHudPanels)typeof(RovHud).GetField("panels",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(hud);
                panels.SetHidden(false);
                Check("HUD restores after hide-all",!root.Q("root").ClassListContains("hud-hidden"));
                hud.enabled=false;hud.enabled=true;
                Check("HUD re-enable does not duplicate panel controls",root.Query<Button>("minimize-control").ToList().Count==1);
            }
            finally {rig.SetView(original);rig.SendMessage("LateUpdate");}
            var report=string.Join("\n",results);
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath,"../Docs/ViewCheckResults.txt"),report);
            return report;
        }
    }
}
