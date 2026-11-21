using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ClubROV.Simulation
{
    public sealed class RovHudPanels : IDisposable
    {
        private readonly VisualElement root, dock, drawer, status;
        private readonly ScrollView scroll;
        private readonly Label drawerTitle, armSummary, telemetrySummary;
        private readonly Label armSource, telemetrySource;
        private readonly Label tetherSource, tetherSummary;
        private readonly Button reveal;
        private readonly List<Action> cleanup = new();
        private readonly Dictionary<string, VisualElement> panels = new();
        private readonly Dictionary<string, Button> buttons = new();
        public bool Hidden { get; private set; }
        public string ActivePanel { get; private set; }

        public RovHudPanels(VisualElement document)
        {
            root = document.Q<VisualElement>("root");
            root.focusable = true;
            root.AddToClassList("hud-docked");
            armSource = document.Q<Label>("arming");
            telemetrySource = document.Q<Label>("telemetry");
            tetherSource = document.Q<Label>("tether-status");
            dock = new VisualElement { name = "hud-dock" };
            dock.AddToClassList("hud-dock");
            dock.Add(new Label("ROV") { name = "dock-brand" });
            root.Add(dock);

            drawer = new VisualElement { name = "hud-drawer" };
            drawer.AddToClassList("hud-drawer");
            var heading = new VisualElement();
            heading.AddToClassList("drawer-heading");
            drawerTitle = new Label();
            heading.Add(drawerTitle);
            var close = new Button(() => OpenPanel(null)) { name = "close-panel", text = "CLOSE ×", focusable = false };
            heading.Add(close);
            drawer.Add(heading);
            scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("drawer-scroll");
            drawer.Add(scroll);
            root.Add(drawer);

            Add(document.Q<VisualElement>(className: "identity"), "CONTROLS", "control", "Camera and SITL connection");
            Add(document.Q<VisualElement>(className: "course-panel"), "COURSE", "course", "Training maps, conditions, scenarios, objectives and scoring");
            Add(document.Q<VisualElement>(className: "footer"), "VEHICLE", "vehicle", "Telemetry, thrusters, graphics and control reference");
            var debug = document.Q<VisualElement>("debug-panel");
            if (debug != null) Add(debug, "DEBUG", "debug", "Live sensor measurements and simulation diagnostics");
            var hide = new Button(() => SetHidden(true)) { name = "hide-hud", text = "HIDE · H", focusable = false, tooltip = "Hide panels and status · H / right-stick click" };
            hide.AddToClassList("dock-button");
            dock.Add(hide);
            reveal = new Button(() => SetHidden(false)) { name = "show-hud", text = "MENU", focusable = false };
            reveal.AddToClassList("hud-reveal");
            root.Add(reveal);

            status = new VisualElement { name = "hud-status", pickingMode = PickingMode.Ignore };
            status.AddToClassList("hud-status");
            armSummary = new Label { pickingMode = PickingMode.Ignore };
            armSummary.AddToClassList("status-arm");
            telemetrySummary = new Label { pickingMode = PickingMode.Ignore };
            status.Add(armSummary);
            status.Add(telemetrySummary);
            tetherSummary = new Label { pickingMode = PickingMode.Ignore };
            status.Add(tetherSummary);
            root.Add(status);
            OpenPanel(null);
            SetHidden(false);
        }

        private void Add(VisualElement panel, string title, string id, string tooltip)
        {
            var parent = panel.parent;
            int index = parent.IndexOf(panel);
            var previousDisplay = panel.style.display;
            var children = new List<VisualElement>(panel.Children());
            var content = new VisualElement { name = "panel-content-" + id };
            foreach (var child in children) content.Add(child);
            panel.Add(content);
            panel.AddToClassList("dock-panel");
            scroll.Add(panel);
            panels.Add(id, panel);
            var button = new Button(() => OpenPanel(ActivePanel == id ? null : id))
            { name = "open-" + id, text = title, tooltip = tooltip, focusable = false };
            button.AddToClassList("dock-button");
            buttons.Add(id, button);
            dock.Add(button);
            cleanup.Add(() =>
            {
                foreach (var child in children) panel.Add(child);
                content.RemoveFromHierarchy();
                panel.RemoveFromClassList("dock-panel");
                panel.style.display = previousDisplay;
                parent.Insert(Math.Min(index, parent.childCount), panel);
            });
        }

        public void OpenPanel(string id)
        {
            ActivePanel = id;
            root.EnableInClassList("hud-drawer-open", id != null);
            foreach (var pair in panels)
            {
                bool selected = pair.Key == id;
                pair.Value.style.display = selected ? DisplayStyle.Flex : DisplayStyle.None;
                buttons[pair.Key].EnableInClassList("selected", selected);
            }
            drawer.style.display = Hidden || id == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (id != null)
            {
                drawerTitle.text = buttons[id].text;
                scroll.scrollOffset = UnityEngine.Vector2.zero;
            }
            root.Focus();
        }

        public void SetHidden(bool hidden)
        {
            Hidden = hidden;
            root.EnableInClassList("hud-hidden", hidden);
            drawer.style.display = hidden || ActivePanel == null ? DisplayStyle.None : DisplayStyle.Flex;
            reveal.style.display = hidden ? DisplayStyle.Flex : DisplayStyle.None;
            root.Focus();
        }

        public void Tick()
        {
            if (tetherSource != null && tetherSummary.text != tetherSource.text) tetherSummary.text = tetherSource.text;
            if (armSource != null)
            {
                if (armSummary.text != armSource.text) armSummary.text = armSource.text;
                armSummary.EnableInClassList("armed", armSource.ClassListContains("armed"));
                armSummary.EnableInClassList("disarmed", armSource.ClassListContains("disarmed"));
            }
            if (telemetrySource != null && telemetrySummary.text != telemetrySource.text)
                telemetrySummary.text = telemetrySource.text;
            var focused = root.panel?.focusController?.focusedElement as VisualElement;
            bool editing = focused is TextField || focused?.GetFirstAncestorOfType<TextField>() != null;
            if (!editing && ((Keyboard.current?.hKey.wasPressedThisFrame ?? false) ||
                (Gamepad.current?.rightStickButton.wasPressedThisFrame ?? false))) SetHidden(!Hidden);
            if (!editing && (Keyboard.current?.escapeKey.wasPressedThisFrame ?? false)) OpenPanel(null);
        }

        public void Dispose()
        {
            foreach (var action in cleanup) action();
            cleanup.Clear();
            dock.RemoveFromHierarchy();
            drawer.RemoveFromHierarchy();
            status.RemoveFromHierarchy();
            reveal.RemoveFromHierarchy();
            root.RemoveFromClassList("hud-hidden");
            root.RemoveFromClassList("hud-docked");
            root.RemoveFromClassList("hud-drawer-open");
        }
    }
}
