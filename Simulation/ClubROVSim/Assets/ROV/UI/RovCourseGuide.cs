using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClubROV.Simulation
{
    public sealed class RovCourseGuide : IDisposable
    {
        private readonly RovTrainingCourse course;
        private readonly RovDynamics vehicle;
        private readonly RovTrainingMaps maps;
        private readonly Camera camera;
        private readonly RovCameraRig cameraRig;
        private readonly VisualElement root, card, steps, menuSteps;
        private readonly Label title, instruction, navigation, hold, toast, briefing, waypoint;
        private readonly ProgressBar progress, menuProgress;
        private Label[] rows = Array.Empty<Label>(), menuRows = Array.Empty<Label>();
        private int revision = -1, completed;
        private double toastUntil;

        public RovCourseGuide(VisualElement document, RovTrainingCourse trainingCourse, RovDynamics dynamics, Camera view, Action openCourse)
        {
            course = trainingCourse; vehicle = dynamics; camera = view;
            cameraRig = view == null ? null : view.GetComponent<RovCameraRig>();
            maps = course == null ? null : course.GetComponent<RovTrainingMaps>();
            root = document.Q<VisualElement>("root");
            briefing = document.Q<Label>("course-briefing");
            menuSteps = document.Q<VisualElement>("course-checklist");
            menuProgress = document.Q<ProgressBar>("course-progress");
            card = new VisualElement { name = "course-guide", pickingMode = PickingMode.Ignore };
            card.AddToClassList("course-guide");
            var heading = new VisualElement { pickingMode = PickingMode.Ignore }; heading.AddToClassList("guide-heading"); card.Add(heading);
            title = new Label { name = "guide-title", pickingMode = PickingMode.Ignore }; heading.Add(title);
            var details = new Button(() => openCourse?.Invoke()) { text = "COURSE", focusable = false }; heading.Add(details);
            instruction = Text(card, "guide-instruction", "guide-instruction");
            navigation = Text(card, "guide-navigation", "guide-caption");
            hold = Text(card, "guide-hold", "guide-hold");
            progress = new ProgressBar { lowValue = 0, highValue = 1, pickingMode = PickingMode.Ignore };
            progress.AddToClassList("guide-progress"); card.Add(progress);
            steps = new VisualElement { pickingMode = PickingMode.Ignore }; steps.AddToClassList("guide-checklist"); card.Add(steps);
            toast = Text(card, "guide-toast", "guide-toast"); toast.AddToClassList("guide-empty");
            root.Add(card);
            waypoint = new Label { name = "course-waypoint", pickingMode = PickingMode.Ignore };
            waypoint.AddToClassList("course-waypoint"); root.Add(waypoint);
            Tick();
        }

        private static Label Text(VisualElement parent, string name, string className)
        {
            var label = new Label { name = name, pickingMode = PickingMode.Ignore };
            label.AddToClassList(className); parent.Add(label); return label;
        }
        private void BuildSteps()
        {
            steps.Clear(); menuSteps?.Clear();
            rows = new Label[course.ObjectiveCount]; menuRows = new Label[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = Text(steps, "guide-step-" + i, "guide-step");
                if (menuSteps != null) menuRows[i] = Text(menuSteps, "course-step-" + i, "guide-step");
            }
        }

        public void Tick()
        {
            if (course == null || vehicle == null) { card.AddToClassList("guide-empty"); waypoint.AddToClassList("guide-empty"); return; }
            int count = course.ObjectiveCount, done = course.ObjectivesCompleted;
            card.EnableInClassList("guide-fpv", cameraRig != null && cameraRig.PilotView);
            if (revision != course.ResetRevision || rows.Length != count)
            {
                revision = course.ResetRevision; completed = done; toastUntil = 0;
                BuildSteps();
            }
            else if (done > completed)
            {
                toast.text = course.Complete ? $"ALL {count} OBJECTIVES COMPLETE!" : $"COMPLETED · {done} / {count}";
                toastUntil = Time.realtimeSinceStartupAsDouble + 5;
                completed = done;
            }
            toast.EnableInClassList("guide-empty", Time.realtimeSinceStartupAsDouble >= toastUntil);
            string map = maps != null && maps.Current >= 0 ? RovTrainingMaps.Names[maps.Current] : "TRAINING";
            title.text = course.ReviewEnded ? "PRACTICE ENDED" : course.Complete ? "COURSE COMPLETE" : map.ToUpperInvariant();
            instruction.text = course.PilotInstruction;
            if (briefing != null) briefing.text = course.Briefing;
            card.EnableInClassList("guide-complete", course.Complete);
            progress.highValue = Mathf.Max(1, count); progress.value = done;
            progress.title = count > 0 ? $"{done} / {count} objectives completed" : "EXPLORATION · no automatic objectives";
            if (menuProgress != null) { menuProgress.highValue = progress.highValue; menuProgress.value = done; menuProgress.title = progress.title; }
            for (int i = 0; i < rows.Length; i++)
            {
                string state = i < done ? "DONE" : i == done && !course.ReviewEnded ? "NOW" : "NEXT";
                string text = $"{state}  {i + 1}. {course.ObjectiveStep(i)}";
                void UpdateRow(Label row)
                {
                    if (row == null) return;
                    row.text = text; row.EnableInClassList("guide-done", i < done);
                    row.EnableInClassList("guide-current", i == done && !course.ReviewEnded);
                }
                UpdateRow(rows[i]); UpdateRow(menuRows[i]);
            }
            bool active = course.Configured && !course.Complete && !course.ReviewEnded;
            if (active)
            {
                Vector3 position = vehicle.Body == null ? vehicle.transform.position : vehicle.Body.position;
                Vector3 delta = course.TargetPosition - position;
                float bearing = Mathf.Repeat(Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, 360);
                navigation.text = $"Target {delta.magnitude:0.0} m away · bearing {bearing:000}° · target depth {-course.TargetPosition.y:0.00} m";
                if (course.HoldingStage)
                {
                    var status = course.CheckHold(position, vehicle.Body == null ? vehicle.transform.rotation : vehicle.Body.rotation,
                        vehicle.Body == null ? Vector3.zero : vehicle.Body.linearVelocity);
                    string speedLimit = course.HoverComplete ? "0.10" : "0.15";
                    hold.text = status.Ready ? $"HOLDING {Mathf.Min(3, course.HoldSeconds):0.0} / 3.0 s — keep steady"
                        : $"Hold not counting · position {(status.position ? "OK" : "move into target")} · heading {(status.heading ? "OK" : "align ±15°")} · speed {(status.speed ? "OK" : "slow below " + speedLimit + " m/s")}";
                    hold.RemoveFromClassList("guide-empty");
                }
                else hold.AddToClassList("guide-empty");
            }
            else
            {
                navigation.text = course.ReviewEnded || course.Complete ? "COURSE → VIEW LAST RUN REVIEW · restart for another run"
                    : "Explore freely. COURSE → END PRACTICE & REVIEW when finished.";
                hold.AddToClassList("guide-empty");
            }
            bool showWaypoint = active && camera != null && root.panel != null;
            if (showWaypoint)
            {
                Vector3 screen = camera.WorldToScreenPoint(course.TargetPosition);
                showWaypoint = screen.z > 0 && screen.x > 0 && screen.x < Screen.width && screen.y > 0 && screen.y < Screen.height;
                if (showWaypoint)
                {
                    Vector2 point = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
                    waypoint.style.left = point.x - 60; waypoint.style.top = point.y - 32;
                    waypoint.text = $"▼ TARGET\n{Vector3.Distance(vehicle.transform.position, course.TargetPosition):0.0} m";
                }
            }
            waypoint.EnableInClassList("guide-empty", !showWaypoint);
        }

        public void Dispose()
        {
            card.RemoveFromHierarchy(); waypoint.RemoveFromHierarchy(); menuSteps?.Clear();
        }
    }
}
