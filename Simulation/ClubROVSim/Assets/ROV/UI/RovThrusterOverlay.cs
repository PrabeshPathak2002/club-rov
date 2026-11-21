using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClubROV.Simulation
{
    public sealed class RovThrusterOverlay : IDisposable
    {
        private readonly VisualElement panel, plot;
        private readonly Label[] labels = new Label[6];
        private readonly float[] outputs = new float[6];
        private readonly RovDynamics vehicle;
        // Vectored frame diagram positions, matching RovSitlProtocol.Apply's channel mapping.
        private static readonly int[] MotorNumbers = { 2, 1, 4, 3, 6, 5 };

        public RovThrusterOverlay(VisualElement root, RovDynamics dynamics)
        {
            vehicle = dynamics;
            panel = new VisualElement { name = "thruster-overlay", pickingMode = PickingMode.Ignore };
            panel.AddToClassList("thruster-overlay");
            panel.Add(new Label("THRUSTERS · BLUEROV2 / TOP VIEW"));
            plot = new VisualElement { pickingMode = PickingMode.Ignore };
            plot.AddToClassList("thruster-plot");
            plot.generateVisualContent += Draw;
            panel.Add(plot);
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = new Label { pickingMode = PickingMode.Ignore };
                labels[i].AddToClassList("thruster-value");
                labels[i].AddToClassList("thruster-slot-" + i);
                plot.Add(labels[i]);
            }
            var caption = new Label("ArduSub Vectored reference · bow ↑\nLive thrust: cyan + / amber − · 0% idle\nV: dot = up · cross = down\nGreen/blue housings: reference prop groups");
            caption.AddToClassList("map-caption");
            panel.Add(caption);
            root.Q<VisualElement>("root").Add(panel);
            Tick();
        }

        public void Tick()
        {
            for (int i = 0; i < outputs.Length; i++)
            {
                outputs[i] = vehicle == null ? 0 : vehicle.Output(i);
                string id = i < 4 ? "H" + (i + 1) : "V" + (i - 3);
                labels[i].text = $"M{MotorNumbers[i]} / {id} {outputs[i] * 100:+0;-0;0}%";
                labels[i].EnableInClassList("thrust-positive", outputs[i] > 0.005f);
                labels[i].EnableInClassList("thrust-negative", outputs[i] < -0.005f);
            }
            plot.MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            var p = context.painter2D;
            float width = plot.contentRect.width, height = plot.contentRect.height;
            if (width < 1 || height < 1) return;
            Vector2 centre = new(width / 2, height / 2);
            float scale = Mathf.Min(width / 254, height / 260);
            Vector2 Point(float x, float y) => centre + new Vector2(x, y) * scale;
            void Line(Vector2 a, Vector2 b)
            {
                p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke();
            }
            void Polygon(Vector2[] points, Color fill, Color outline)
            {
                p.fillColor = fill; p.strokeColor = outline; p.BeginPath(); p.MoveTo(points[0]);
                for (int i = 1; i < points.Length; i++) p.LineTo(points[i]);
                p.ClosePath(); p.Fill(); p.Stroke();
            }
            Color edge = new(0.13f, 0.20f, 0.23f);
            p.lineWidth = 2 * scale;
            p.fillColor = new Color(0.24f, 0.29f, 0.31f); p.strokeColor = new Color(0.56f, 0.64f, 0.67f);
            // Rounded rectangular deck and bow marker, inspired by the ArduSub Vectored diagram.
            p.BeginPath();
            p.MoveTo(Point(-66, -116)); p.LineTo(Point(66, -116));
            p.QuadraticCurveTo(Point(90, -116), Point(90, -92)); p.LineTo(Point(90, 92));
            p.QuadraticCurveTo(Point(90, 116), Point(66, 116)); p.LineTo(Point(-66, 116));
            p.QuadraticCurveTo(Point(-90, 116), Point(-90, 92)); p.LineTo(Point(-90, -92));
            p.QuadraticCurveTo(Point(-90, -116), Point(-66, -116)); p.ClosePath(); p.Fill(); p.Stroke();
            Polygon(new[] { Point(0, -105), Point(-10, -82), Point(10, -82) }, new Color(1, 0.36f, 0.2f), edge);
            for (int i = 0; i < outputs.Length; i++)
            {
                p.lineWidth = 2 * scale;
                float value = outputs[i];
                float x = i == 0 || i == 2 || i == 4 ? -55 : 55;
                float y = i < 2 ? -66 : i < 4 ? 66 : 0;
                Vector2 at = Point(x, y);
                Color housing = i < 2 || i == 5 ? new Color(0.18f, 0.65f, 0.37f) : new Color(0.24f, 0.70f, 0.86f);
                Vector3 direction = RovThrusterLayout.Direction(i);
                Vector2 axis = i < 4 ? new Vector2(direction.x, -direction.z) : Vector2.up;
                Vector2 across = new(-axis.y, axis.x);
                if (i < 4)
                {
                    // Propeller shroud behind the diagonal motor barrel.
                    p.fillColor = housing * 0.7f; p.strokeColor = edge;
                    p.BeginPath(); p.Arc(at, 23 * scale, Angle.Degrees(0), Angle.Degrees(360)); p.Fill(); p.Stroke();
                    Polygon(new[] { at + (across * 25 + axis * 11) * scale, at + (across * 25 - axis * 11) * scale,
                        at + (-across * 25 - axis * 11) * scale, at + (-across * 25 + axis * 11) * scale }, housing, edge);
                    p.strokeColor = edge;
                    Line(at - across * 21 * scale, at + across * 21 * scale);
                    Line(at + axis * 7 * scale - across * 21 * scale, at + axis * 7 * scale + across * 21 * scale);
                }
                else
                {
                    p.fillColor = new Color(0.10f, 0.16f, 0.19f); p.strokeColor = housing;
                    p.BeginPath(); p.Arc(at, 23 * scale, Angle.Degrees(0), Angle.Degrees(360)); p.Fill(); p.Stroke();
                    for (int blade = 0; blade < 5; blade++)
                    {
                        float angle = blade * Mathf.PI * 2 / 5;
                        Vector2 a = new(Mathf.Cos(angle), Mathf.Sin(angle)); Vector2 b = new(-a.y, a.x);
                        Polygon(new[] { at + a * 4 * scale, at + (a * 19 + b * 7) * scale,
                            at + (a * 18 - b * 2) * scale }, housing, edge);
                    }
                    p.fillColor = housing; p.BeginPath(); p.Arc(at, 7 * scale, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
                }
                p.strokeColor = Mathf.Abs(value) < 0.005f ? new Color(0.65f, 0.73f, 0.77f)
                    : value > 0 ? new Color(0.42f, 0.95f, 0.82f) : new Color(1, 0.69f, 0.32f);
                p.lineWidth = (Mathf.Abs(value) < 0.005f ? 1 : 3) * scale;
                p.BeginPath(); p.Arc(at, 27 * scale, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
                if (Mathf.Abs(value) < 0.005f) continue;
                if (i >= 4)
                {
                    if (value > 0) { p.fillColor = p.strokeColor; p.BeginPath(); p.Arc(at, 4 * scale, Angle.Degrees(0), Angle.Degrees(360)); p.Fill(); }
                    else { Line(at + new Vector2(-5, -5) * scale, at + new Vector2(5, 5) * scale); Line(at + new Vector2(5, -5) * scale, at + new Vector2(-5, 5) * scale); }
                    continue;
                }
                axis *= Mathf.Sign(value);
                Vector2 end = at + axis * (17 + 18 * Mathf.Abs(value)) * scale;
                Line(at, end);
                Vector2 side = new(-axis.y, axis.x);
                Line(end, end + (-axis * 6 + side * 4) * scale);
                Line(end, end + (-axis * 6 - side * 4) * scale);
                p.lineWidth = 2 * scale;
            }
        }

        public void Dispose()
        {
            plot.generateVisualContent -= Draw;
            panel.RemoveFromHierarchy();
        }
    }
}
