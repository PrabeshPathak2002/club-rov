using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ClubROV.Simulation
{
    public sealed class RovHudPanels : IDisposable
    {
        private readonly VisualElement root;
        private readonly List<Action> cleanup = new();
        public bool Hidden { get; private set; }
        public RovHudPanels(VisualElement document)
        {
            root=document.Q<VisualElement>("root"); root.focusable=true;
            Add(document.Q<VisualElement>(className:"identity"),"CONTROL & SETTINGS","control");
            Add(document.Q<VisualElement>(className:"course-panel"),"COURSE & TELEMETRY","course");
            Add(document.Q<VisualElement>(className:"footer"),"VEHICLE & GRAPHICS","vehicle");
            var hide=new Button(()=>SetHidden(true)){name="hide-hud",text="HIDE ALL · H / RIGHT-STICK CLICK",focusable=false};
            hide.AddToClassList("hud-hide"); root.Add(hide);
            cleanup.Add(()=>hide.RemoveFromHierarchy());
        }
        private void Add(VisualElement panel,string title,string id)
        {
            var content=new VisualElement {name="panel-content-"+id};
            var children=new List<VisualElement>(panel.Children());
            foreach(var child in children) content.Add(child);
            panel.Add(content);
            bool minimized=false;
            var toggle=new Button {name="minimize-"+id,focusable=false};
            toggle.text="−  "+title; toggle.AddToClassList("panel-toggle");
            Action clicked=()=>
            {
                minimized=!minimized;
                content.style.display=minimized?DisplayStyle.None:DisplayStyle.Flex;
                panel.EnableInClassList("panel-minimized",minimized);
                toggle.text=(minimized?"+  ":"−  ")+title;
                root.Focus();
            };
            toggle.clicked+=clicked; panel.Insert(0,toggle);
            cleanup.Add(()=>
            {
                toggle.clicked-=clicked; toggle.RemoveFromHierarchy();
                foreach(var child in children) panel.Add(child);
                content.RemoveFromHierarchy(); panel.RemoveFromClassList("panel-minimized");
            });
        }
        public void SetHidden(bool hidden)
        {
            Hidden=hidden; root.EnableInClassList("hud-hidden",hidden);
            root.Focus();
        }
        public void Tick()
        {
            var focused=root.panel?.focusController?.focusedElement as VisualElement;
            bool editing=focused is TextField || focused?.GetFirstAncestorOfType<TextField>()!=null;
            if(!editing && ((Keyboard.current?.hKey.wasPressedThisFrame??false) ||
                (Gamepad.current?.rightStickButton.wasPressedThisFrame??false))) SetHidden(!Hidden);
        }
        public void Dispose()
        {
            root.RemoveFromClassList("hud-hidden");
            foreach(var action in cleanup) action(); cleanup.Clear();
        }
    }
}
