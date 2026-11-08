using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // The name screen's glass pawn (R84): the gold-outlined pawn from the design
    // page turning once around its upright axis every 24 seconds, like a thick
    // token. Each of 15 slices of its gold edge slides sideways by depth × sin and
    // narrows by cos; the face nearest the viewer is drawn on top, with a soft
    // glint as it catches the light from the front left, and the edge seen through
    // the glass is dimmed. Lines keep their width whatever the turn. Translucent
    // colours go through IntroFlowStyle.Light (linear colour space, PITFALLS 30).
    // Drawn in the design's coordinates: this element sits at (680, 120), 500 × 530.
    public sealed class GlassPawn : VisualElement
    {
        const float TurnSeconds = 24f, Depth = 1.4f, Scale = 9.2f;
        const int Slices = 15;
        static readonly Vector2 Centre = new Vector2(930f - 680f, 400f - 120f);
        static readonly Vector2 RingCentre = new Vector2(930f - 680f, 372f - 120f);
        // The pawn's height in its own units (top of the head to the base), for
        // gradients that run down the whole piece.
        const float Top = -23f, Bottom = 26f;

        static readonly (float at, Color c)[] Edge = { (0f, Hex(0xA27A3E)), (.5f, Hex(0x7A5627)), (1f, Hex(0x4E3316)) };
        static readonly (float at, Color c)[] Line = { (0f, Hex(0xFFEFC2)), (.5f, Hex(0xE2B866)), (1f, Hex(0x9C6E2A)) };
        const int Bands = 10;

        static List<Vector2[]> shapes;
        readonly float startedAt;

        public GlassPawn()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
            startedAt = Time.unscaledTime;
            generateVisualContent += Paint;
        }

        // The pawn as outlines in its own units: head, collar, body, base.
        static List<Vector2[]> Shapes()
        {
            if (shapes != null) return shapes;
            shapes = new List<Vector2[]>
            {
                Circle(new Vector2(0, -14), 9f, 48),
                RoundRect(-10, -6, 20, 4, 2),
                Body(),
                RoundRect(-16, 19, 32, 7, 3),
            };
            return shapes;
        }

        void Paint(MeshGenerationContext context)
        {
            var p = context.painter2D;
            float angle = (Time.unscaledTime - startedAt) / TurnSeconds * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            // Never quite flat: a flat outline would vanish for that frame.
            if (Mathf.Abs(cos) < 1e-3f) cos = cos < 0 ? -1e-3f : 1e-3f;
            var all = Shapes();

            // The near face: z = +depth while it faces this way, else the back.
            float near = cos >= 0 ? Depth : -Depth;
            var face = new List<Vector2[]>(all.Count);
            foreach (var s in all) face.Add(Place(s, cos, sin, near));

            // The edge, slice by slice; where a slice runs behind the face it is
            // seen through the glass and dimmed.
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;
            p.lineWidth = 2.9f;
            int slices = Mathf.Abs(sin) < .02f ? 1 : Slices;
            for (int k = 0; k < slices; k++)
            {
                float z = slices == 1 ? near : -Depth + 2f * Depth * k / (Slices - 1);
                if (slices > 1 && Mathf.Approximately(z, near)) continue;
                for (int i = 0; i < all.Count; i++)
                    StrokeBanded(p, all[i], Place(all[i], cos, sin, z), Edge, face);
            }

            // The face: a glint, the faint gold glass, the gold line.
            float glint = Mathf.Max(0f, .26f * Mathf.Pow(Mathf.Max(0f, Mathf.Sign(cos) * Mathf.Cos(angle + .7f)), 8f) - .031f);
            var glass = FillGradient.MakeLinearGradient(Gradient((0f, new Color(1f, .906f, .69f, IntroFlowStyle.Light(.2f))), (1f, new Color(.788f, .604f, .353f, IntroFlowStyle.Light(.03f)))),
                                                        new Vector2(0, Centre.y + Top * Scale), new Vector2(0, Centre.y + Bottom * Scale), AddressMode.Clamp);
            foreach (var outline in face)
            {
                if (glint > 0f) Fill(p, outline, new Color(1f, .941f, .8f, IntroFlowStyle.Light(glint)));
                p.fillGradient = glass;
                Path(p, outline);
                p.Fill();
            }
            p.lineWidth = 2.94f;
            for (int i = 0; i < all.Count; i++) StrokeBanded(p, all[i], face[i], Line, null);

            // The two rings around it.
            p.lineWidth = 2f;
            p.strokeColor = new Color(226 / 255f, 184 / 255f, 102 / 255f, IntroFlowStyle.Light(.42f));
            p.BeginPath(); p.Arc(RingCentre, 228f, 0f, 360f); p.Stroke();
            p.lineWidth = 1f;
            p.strokeColor = new Color(226 / 255f, 184 / 255f, 102 / 255f, IntroFlowStyle.Light(.16f));
            p.BeginPath(); p.Arc(RingCentre, 242f, 0f, 360f); p.Stroke();
        }

        // x' = x cos + z sin (a turn about the upright axis), in this element's pixels.
        static Vector2[] Place(Vector2[] shape, float cos, float sin, float z)
        {
            var placed = new Vector2[shape.Length];
            for (int i = 0; i < shape.Length; i++)
                placed[i] = Centre + new Vector2((shape[i].x * cos + z * sin) * Scale, shape[i].y * Scale);
            return placed;
        }

        // A closed outline stroked in runs of one colour from a gradient down the
        // piece (Painter2D strokes in one colour); runs under the face are dimmed.
        static void StrokeBanded(Painter2D p, Vector2[] local, Vector2[] placed, (float at, Color c)[] stops, List<Vector2[]> under)
        {
            int n = placed.Length;
            int start = 0;
            while (start < n)
            {
                int band = BandOf(local, start);
                bool dim = under != null && Covered(placed, start, under);
                int end = start + 1;
                while (end < n && BandOf(local, end) == band && (under == null || Covered(placed, end, under) == dim)) end++;
                var c = Sample(stops, (band + .5f) / Bands);
                if (dim) c.a *= IntroFlowStyle.Light(.3f);
                p.strokeColor = c;
                p.BeginPath();
                p.MoveTo(placed[start]);
                for (int i = start + 1; i <= end; i++) p.LineTo(placed[i % n]);
                p.Stroke();
                start = end;
            }
        }

        static int BandOf(Vector2[] local, int i)
        {
            var a = local[i];
            var b = local[(i + 1) % local.Length];
            float t = ((a.y + b.y) * .5f - Top) / (Bottom - Top);
            return Mathf.Clamp((int)(t * Bands), 0, Bands - 1);
        }

        static bool Covered(Vector2[] placed, int i, List<Vector2[]> face)
        {
            var mid = (placed[i] + placed[(i + 1) % placed.Length]) * .5f;
            foreach (var f in face) if (Inside(f, mid)) return true;
            return false;
        }

        static bool Inside(Vector2[] v, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
                if ((v[i].y > p.y) != (v[j].y > p.y) && p.x < (v[j].x - v[i].x) * (p.y - v[i].y) / (v[j].y - v[i].y) + v[i].x)
                    inside = !inside;
            return inside;
        }

        static void Path(Painter2D p, Vector2[] outline)
        {
            p.BeginPath();
            p.MoveTo(outline[0]);
            for (int i = 1; i < outline.Length; i++) p.LineTo(outline[i]);
            p.ClosePath();
        }

        static void Fill(Painter2D p, Vector2[] outline, Color c)
        {
            p.fillGradient = FillGradient.MakeLinearGradient(Gradient((0f, c), (1f, c)), Vector2.zero, Vector2.right, AddressMode.Clamp);
            Path(p, outline);
            p.Fill();
        }

        static Vector2[] Circle(Vector2 c, float r, int steps)
        {
            var points = new Vector2[steps];
            for (int i = 0; i < steps; i++)
            {
                float a = i / (float)steps * Mathf.PI * 2f;
                points[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            return points;
        }

        static Vector2[] RoundRect(float x, float y, float w, float h, float r)
        {
            var points = new List<Vector2>();
            void Corner(float cx, float cy, float from)
            {
                for (int i = 0; i <= 6; i++)
                {
                    float a = (from + i * 15f) * Mathf.Deg2Rad;
                    points.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
                }
            }
            Corner(x + w - r, y + r, -90f);
            Corner(x + w - r, y + h - r, 0f);
            Corner(x + r, y + h - r, 90f);
            Corner(x + r, y + r, 180f);
            return points.ToArray();
        }

        // M-7 -2 Q-8 12 -14 20 L14 20 Q8 12 7 -2 Z
        static Vector2[] Body()
        {
            var points = new List<Vector2> { new Vector2(-7, -2) };
            Quad(points, new Vector2(-7, -2), new Vector2(-8, 12), new Vector2(-14, 20));
            points.Add(new Vector2(14, 20));
            Quad(points, new Vector2(14, 20), new Vector2(8, 12), new Vector2(7, -2));
            return points.ToArray();
        }

        static void Quad(List<Vector2> points, Vector2 a, Vector2 c, Vector2 b)
        {
            for (int i = 1; i <= 16; i++)
            {
                float t = i / 16f;
                points.Add((1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b);
            }
        }

        static Gradient Gradient(params (float at, Color c)[] stops)
        {
            var g = new Gradient();
            var colors = new GradientColorKey[stops.Length];
            var alphas = new GradientAlphaKey[stops.Length];
            for (int i = 0; i < stops.Length; i++)
            {
                colors[i] = new GradientColorKey(stops[i].c, stops[i].at);
                alphas[i] = new GradientAlphaKey(stops[i].c.a, stops[i].at);
            }
            g.SetKeys(colors, alphas);
            return g;
        }

        static Color Sample((float at, Color c)[] stops, float t)
        {
            if (t <= stops[0].at) return stops[0].c;
            for (int i = 1; i < stops.Length; i++)
                if (t <= stops[i].at) return Color.Lerp(stops[i - 1].c, stops[i].c, (t - stops[i - 1].at) / Mathf.Max(1e-5f, stops[i].at - stops[i - 1].at));
            return stops[stops.Length - 1].c;
        }

        static Color Hex(int rgb) => new Color((rgb >> 16 & 255) / 255f, (rgb >> 8 & 255) / 255f, (rgb & 255) / 255f);
    }
}
