using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClubROV.Simulation
{
    public sealed class RovRunReviewPanel : IDisposable
    {
        private readonly RovTrainingCourse course;
        private readonly VisualElement root, overlay, plot;
        private readonly Label subtitle, metrics, comparison, effort, legend;
        private readonly ProgressBar[] bars = new ProgressBar[6];
        private readonly Button finish, last;
        private readonly Action revealHud;
        private RovRunReview review;

        public RovRunReviewPanel(VisualElement document, RovTrainingCourse trainingCourse, Action reveal = null)
        {
            course = trainingCourse;
            revealHud = reveal;
            root = document.Q<VisualElement>("root");
            finish = document.Q<Button>("review-finish");
            last = document.Q<Button>("review-last");
            if (finish != null) finish.clicked += Finish;
            if (last != null) last.clicked += ShowLast;
            overlay = new VisualElement { name = "run-review" };
            overlay.AddToClassList("run-review"); overlay.AddToClassList("review-closed");
            var panel = new VisualElement(); panel.AddToClassList("review-panel"); overlay.Add(panel);
            var heading = new VisualElement(); heading.AddToClassList("review-heading"); panel.Add(heading);
            heading.Add(new Label("PILOT RUN REVIEW"));
            var close = new Button(Close) { name = "review-close", text = "CLOSE ×", focusable = false }; heading.Add(close);
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("review-scroll"); panel.Add(scroll);
            subtitle = Text(scroll, "review-subtitle");
            metrics = Text(scroll, "review-metrics");
            comparison = Text(scroll, "review-comparison");
            Text(scroll, "review-caption").text = "ROUTE · TOP DOWN · +Z UP · SIMULATOR TRACE (not ROS navigation)";
            plot = new VisualElement { name = "review-route", pickingMode = PickingMode.Ignore };
            plot.AddToClassList("review-route"); plot.generateVisualContent += DrawRoute; scroll.Add(plot);
            legend = Text(scroll, "review-caption");
            Text(scroll, "review-metrics").text = "THRUSTER USE · mean absolute output / peak / direction reversals";
            for (int i = 0; i < bars.Length; i++)
            {
                bars[i] = new ProgressBar { lowValue = 0, highValue = 100 };
                bars[i].AddToClassList("review-thrust"); scroll.Add(bars[i]);
            }
            effort = Text(scroll, "review-caption");
            Text(scroll, "review-caption").text = "Review does not pause motion or disarm SITL. Close and restart to record another run.";
            root.Add(overlay); root.RegisterCallback<KeyDownEvent>(OnKey);
            if (course != null) course.ReviewReady += Show;
            Tick();
        }

        private static Label Text(VisualElement parent, string className)
        {
            var label = new Label(); label.AddToClassList(className); parent.Add(label); return label;
        }
        private void Finish() => course?.EndPracticeReview();
        private void ShowLast() { if (course?.LastReview != null) Show(course.LastReview); }
        private void Close() { overlay.AddToClassList("review-closed"); root.Focus(); }
        private void OnKey(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape && !overlay.ClassListContains("review-closed")) { Close(); evt.StopPropagation(); }
        }

        public void Tick()
        {
            finish?.SetEnabled(course != null && course.CanEndPractice);
            last?.SetEnabled(course?.LastReview != null);
        }

        public void Show(RovRunReview value)
        {
            review = value;
            var r = value.result; var f = r.feedback;
            string map = f.map >= 0 && f.map < RovTrainingMaps.Names.Length ? RovTrainingMaps.Names[f.map] : "Training map";
            subtitle.text = $"{map} · {r.scenarioName}\n{(f.completed ? "COURSE COMPLETED" : "PRACTICE ENDED · course not completed")}";
            string stability = f.scoredCourse ? $"Depth RMS {r.depthRms:0.00} m · Heading RMS {r.headingRms:0.0}°" : "Depth / heading target errors: N/A (free exploration)";
            metrics.text = $"{(f.completed && f.qualified ? $"Score {r.score} · " : "")}{TimeSpan.FromSeconds(r.seconds):mm\\:ss\\.f} · {f.distance:0.0} m travelled · {r.collisions} collisions\n{stability}";
            var p = value.previous;
            comparison.text = !f.qualified ? "Conditions or control mode changed during this run: practice only; comparison unavailable."
                : p == null ? "First recorded run of this type with these conditions. Your next matching run will show a comparison."
                : $"VERSUS PREVIOUS {(f.completed ? "COMPLETED COURSE" : "ENDED PRACTICE")} · same conditions/control mode\n" +
                  $"Time {r.seconds - p.seconds:+0.0;-0.0;0.0} s · collisions {r.collisions - p.collisions:+0;-0;0}" +
                  (f.completed ? $" · score {r.score - p.score:+0;-0;0}" : " (practice durations may differ)") +
                  (f.scoredCourse ? $"\nDepth RMS {r.depthRms - p.depthRms:+0.00;-0.00;0.00} m · heading RMS {r.headingRms - p.headingRms:+0.0;-0.0;0.0}° (lower is better)" : "");
            legend.text = $"Cyan: route · green: start · white: finish · red: collision locations ({value.impacts.Length}/{value.totalImpacts} shown).";
            for (int i = 0; i < bars.Length; i++)
            {
                string id = i < 4 ? "H" + (i + 1) : "V" + (i - 3);
                bars[i].value = f.meanThrust[i] * 100;
                string delta = p?.feedback?.version == 1 && p.feedback.meanThrust?.Length == 6
                    ? $"   vs prior {(f.meanThrust[i] - p.feedback.meanThrust[i]) * 100:+0;-0;0} pp" : "";
                bars[i].title = $"{id}   mean {f.meanThrust[i] * 100:0}%   peak {f.peakThrust[i] * 100:0}%   reversals {f.reversals[i]}{delta}";
            }
            float percent = r.seconds > 0 ? f.lowSpeedThrustSeconds / r.seconds * 100 : 0;
            string previousEffort = p?.feedback == null || p.feedback.version != 1 ? "" : $" Previous run: {p.feedback.lowSpeedThrustSeconds:0.0} s.";
            effort.text = $"Low-speed thrust: {f.lowSpeedThrustSeconds:0.0} s ({percent:0}% of run).{previousEffort}\n" +
                "Counts horizontal output ≥10% while speed <0.15 m/s; a station-keeping effort proxy, not proof of unnecessary corrections.\n" +
                (f.scoredCourse ? "RMS errors use the active gate/hover/dock target and include transitions. " : "") +
                "Outputs include thruster derating; percentages represent simulated force fractions, not measured power. Route detail is reduced on long runs.";
            revealHud?.Invoke();
            plot.MarkDirtyRepaint(); overlay.RemoveFromClassList("review-closed"); root.Focus();
        }

        private void DrawRoute(MeshGenerationContext context)
        {
            if (review?.route == null || review.route.Length == 0) return;
            Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity), max = new(float.NegativeInfinity, float.NegativeInfinity);
            void Include(Vector3 p) { min = Vector2.Min(min, new Vector2(p.x, p.z)); max = Vector2.Max(max, new Vector2(p.x, p.z)); }
            foreach (var p in review.route) Include(p);
            foreach (var p in review.impacts) Include(p);
            float width = plot.contentRect.width, height = plot.contentRect.height;
            if (width < 1 || height < 1) return;
            Vector2 centre = (min + max) / 2;
            float scale = Mathf.Min((width - 36) / Mathf.Max(1, max.x - min.x), (height - 36) / Mathf.Max(1, max.y - min.y));
            Vector2 Point(Vector3 p) => new(width / 2 + (p.x - centre.x) * scale, height / 2 - (p.z - centre.y) * scale);
            var painter = context.painter2D;
            painter.lineWidth = 1; painter.strokeColor = new Color(0.12f, 0.27f, 0.32f);
            for (int i = 1; i < 5; i++)
            {
                painter.BeginPath(); painter.MoveTo(new Vector2(width * i / 5, 0)); painter.LineTo(new Vector2(width * i / 5, height)); painter.Stroke();
                painter.BeginPath(); painter.MoveTo(new Vector2(0, height * i / 5)); painter.LineTo(new Vector2(width, height * i / 5)); painter.Stroke();
            }
            painter.lineWidth = 2; painter.strokeColor = new Color(0.42f, 0.94f, 0.84f);
            painter.BeginPath(); painter.MoveTo(Point(review.route[0]));
            for (int i = 1; i < review.route.Length; i++) painter.LineTo(Point(review.route[i]));
            painter.Stroke();
            void Dot(Vector3 position, Color color, float radius)
            {
                painter.fillColor = color; painter.BeginPath(); painter.Arc(Point(position), radius, Angle.Degrees(0), Angle.Degrees(360)); painter.Fill();
            }
            Dot(review.route[0], new Color(0.4f, 1, 0.5f), 5);
            Dot(review.route[review.route.Length - 1], Color.white, 4);
            foreach (var impact in review.impacts) Dot(impact, new Color(1, 0.3f, 0.25f), 4);
        }

        public void Dispose()
        {
            if (course != null) course.ReviewReady -= Show;
            if (finish != null) finish.clicked -= Finish;
            if (last != null) last.clicked -= ShowLast;
            root.UnregisterCallback<KeyDownEvent>(OnKey);
            plot.generateVisualContent -= DrawRoute; overlay.RemoveFromHierarchy();
        }
    }
}
