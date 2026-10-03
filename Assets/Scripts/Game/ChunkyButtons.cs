using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // The menu's slab buttons (design C, Docs/Architecture/UI.md "버튼"): a plate
    // with clipped corners standing on a darker side a few pixels thick. Pressed,
    // it sinks onto the side; released, it springs back past its rest with a
    // flash and a burst of light streaks. Gold is the one main action, ivory a
    // confirmation, walnut secondary, red leave or cancel.
    //
    // Any element with the class "chunky" gets it (usually a Button); add
    // "ch-ivory", "ch-red" or "ch-wood" for the colour and "ch-lg", "ch-sm" or
    // "ch-xs" for the thickness. NetworkHud.uss keeps the matching padding (the
    // side's thickness is part of the bottom padding). Pulse plays the release
    // for clicks that arrive another way (a key, the HUD's fallback router).
    public static class ChunkyButtons
    {
        public const string ClassName = "chunky";

        sealed class State
        {
            public float Offset, Velocity, Target, Flash;
            public bool Down, Hover;
            public IVisualElementScheduledItem Tick;
        }

        struct Look
        {
            public Color Top, Bottom, Side;
            public bool Grain;
            public float Depth, Cut;
        }

        static readonly ConditionalWeakTable<VisualElement, State> states = new ConditionalWeakTable<VisualElement, State>();
        static readonly Color[] Sparks = { Hex(0xFFE07A), Color.white, Hex(0xFFC66B), Hex(0x3CD0FF), Hex(0xFF6B7E) };
        static readonly Color Rim = new Color(217 / 255f, 174 / 255f, 98 / 255f, .4f);

        static Color Hex(int rgb) => PieceFigure.Hex(rgb);

        // Wires every "chunky" element under `root`. Safe to call again after adding more.
        public static void Attach(VisualElement root)
        {
            root?.Query<VisualElement>(className: ClassName).ForEach(Make);
        }

        public static void Make(VisualElement e)
        {
            if (e == null || states.TryGetValue(e, out _)) return;
            var s = new State();
            states.Add(e, s);
            e.AddToClassList(ClassName);
            e.generateVisualContent += Draw;
            // The plate is drawn after the element's own text and would hide it, so
            // the text moves to a child label (it inherits font, size and colour).
            // Text written later is moved across by the poll.
            if (e is TextElement owner)
            {
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.AddToClassList("chunky-text");
                e.Add(label);
                void Move()
                {
                    if (string.IsNullOrEmpty(owner.text)) return;
                    label.text = owner.text;
                    owner.text = "";
                }
                Move();
                e.schedule.Execute(Move).Every(100);
            }
            e.RegisterCallback<PointerEnterEvent>(_ => { s.Hover = true; Retarget(e, s); });
            e.RegisterCallback<PointerLeaveEvent>(_ => { s.Hover = false; s.Down = false; Retarget(e, s); });
            e.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || !e.enabledInHierarchy) return;
                s.Down = true;
                // The press is quick (50 ms on the samples): jump most of the way.
                s.Offset = Mathf.Lerp(s.Offset, Get(e).Depth - 1, .7f);
                s.Velocity = 0;
                Retarget(e, s);
            }, TrickleDown.TrickleDown);
            e.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!s.Down) return;
                s.Down = false;
                Release(e, s, evt.position);
            }, TrickleDown.TrickleDown);
            e.RegisterCallback<PointerCaptureOutEvent>(_ => { if (s.Down) { s.Down = false; Retarget(e, s); } });
        }

        // The release, from code: sink, spring, flash, streaks from the middle.
        public static void Pulse(VisualElement e)
        {
            if (e == null || e.panel == null) return;
            Make(e);
            if (!states.TryGetValue(e, out var s)) return;
            s.Offset = Get(e).Depth - 1;
            Release(e, s, e.worldBound.center);
        }

        static void Release(VisualElement e, State s, Vector2 at)
        {
            s.Velocity = -340f;
            s.Flash = 1f;
            Retarget(e, s);
            Burst(e, at);
        }

        static void Retarget(VisualElement e, State s)
        {
            var look = Get(e);
            s.Target = !e.enabledInHierarchy ? 0 : s.Down ? look.Depth - 1 : s.Hover ? -2 : 0;
            if (s.Tick == null) s.Tick = e.schedule.Execute(() => Step(e, s)).Every(16);
            else s.Tick.Resume();
        }

        static void Step(VisualElement e, State s)
        {
            const float dt = .016f, stiffness = 900f, damping = 22f;
            if (s.Down) s.Offset = Mathf.MoveTowards(s.Offset, s.Target, 400f * dt);
            else
            {
                s.Velocity += ((s.Target - s.Offset) * stiffness - s.Velocity * damping) * dt;
                s.Offset += s.Velocity * dt;
            }
            s.Flash = Mathf.Max(0, s.Flash - dt / .3f);
            e.style.translate = new Translate(0, s.Offset);
            e.MarkDirtyRepaint();
            if (!s.Down && s.Flash <= 0 && Mathf.Abs(s.Offset - s.Target) < .05f && Mathf.Abs(s.Velocity) < .5f)
            {
                s.Offset = s.Target;
                s.Velocity = 0;
                e.style.translate = new Translate(0, s.Offset);
                s.Tick.Pause();
            }
        }

        static Look Get(VisualElement e)
        {
            var look = new Look { Top = Hex(0xFFE07A), Bottom = Hex(0xFFB72A), Side = Hex(0x6E4300), Depth = 6, Cut = 12 };
            if (e.ClassListContains("ch-ivory")) { look.Top = Hex(0xFFF3DA); look.Bottom = Hex(0xE3BF86); look.Side = Hex(0x6E4A26); }
            else if (e.ClassListContains("ch-red")) { look.Top = Hex(0xFF97A3); look.Bottom = Hex(0xE8384C); look.Side = Hex(0x5E0C18); }
            else if (e.ClassListContains("ch-wood")) { look.Top = Hex(0x8C5D37); look.Bottom = Hex(0x563620); look.Side = Hex(0x1E120A); look.Grain = true; }
            if (e.ClassListContains("ch-lg")) { look.Depth = 7; look.Cut = 18; }
            else if (e.ClassListContains("ch-sm")) { look.Depth = 4; look.Cut = 8; }
            else if (e.ClassListContains("ch-xs")) { look.Depth = 3; look.Cut = 6; }
            return look;
        }

        static void Draw(MeshGenerationContext context)
        {
            var e = context.visualElement;
            var size = e.layout.size;
            if (size.x <= 0 || size.y <= 0) return;
            var look = Get(e);
            states.TryGetValue(e, out var s);
            float o = s != null ? s.Offset : 0, flash = s != null ? s.Flash : 0;
            float w = size.x, h = size.y, d = look.Depth, cut = Mathf.Min(look.Cut, (h - d) / 2f);
            float face = h - d;

            Color top = look.Top, bottom = look.Bottom, side = look.Side;
            if (!e.enabledInHierarchy)
            {
                top = Dull(top); bottom = Dull(bottom); side = Dull(side);
            }
            else if (flash > 0)
            {
                top = Color.Lerp(top, Color.white, flash * .45f);
                bottom = Color.Lerp(bottom, Color.white, flash * .3f);
            }

            // The side stays put on screen while the element moves by o.
            Plate(context, w, d - o, face, cut, side, side);
            Plate(context, w, 0, face, cut, top, bottom);
            if (look.Grain)
            {
                for (float y = 2.5f; y < face - 1; y += 5.5f)
                {
                    bool lightLine = ((int)(y / 5.5f)) % 3 == 1;
                    Strip(context, w, face, cut, y, y + 1f, lightLine ? new Color(1f, .84f, .63f, .07f) : new Color(0, 0, 0, .13f));
                }
                var painter = context.painter2D;
                painter.lineWidth = 1f;
                painter.strokeColor = Rim;
                painter.BeginPath();
                Outline(painter, .5f, .5f, w - 1, face - 1, cut);
                painter.ClosePath();
                painter.Stroke();
            }
            // A light edge along the top and a shade along the bottom.
            Strip(context, w, face, cut, 0, 2, new Color(1, 1, 1, look.Grain ? .45f : .7f));
            Strip(context, w, face, cut, face - 2, face, new Color(0, 0, 0, .15f));
        }

        static Color Dull(Color c)
        {
            float g = c.grayscale;
            return Color.Lerp(new Color(g, g, g), Color.black, .35f);
        }

        static void Outline(Painter2D painter, float x, float y, float w, float h, float cut)
        {
            painter.MoveTo(new Vector2(x + cut, y));
            painter.LineTo(new Vector2(x + w, y));
            painter.LineTo(new Vector2(x + w, y + h - cut));
            painter.LineTo(new Vector2(x + w - cut, y + h));
            painter.LineTo(new Vector2(x, y + h));
            painter.LineTo(new Vector2(x, y + cut));
        }

        // The clipped plate from y0 down `h` pixels, a vertical gradient top to bottom.
        static void Plate(MeshGenerationContext context, float w, float y0, float h, float cut, Color top, Color bottom)
        {
            Vector2[] p =
            {
                new Vector2(cut, 0), new Vector2(w, 0), new Vector2(w, h - cut), new Vector2(w - cut, h), new Vector2(0, h), new Vector2(0, cut)
            };
            var mesh = context.Allocate(7, 18);
            var center = new Vector2(w / 2, h / 2);
            mesh.SetNextVertex(Vert(center, y0, Color.Lerp(top, bottom, .5f)));
            for (int i = 0; i < 6; i++) mesh.SetNextVertex(Vert(p[i], y0, Color.Lerp(top, bottom, p[i].y / h)));
            for (int i = 0; i < 6; i++)
            {
                mesh.SetNextIndex(0);
                mesh.SetNextIndex((ushort)(1 + i));
                mesh.SetNextIndex((ushort)(1 + (i + 1) % 6));
            }
        }

        // A horizontal band from y0 to y1 inside the clipped plate.
        static void Strip(MeshGenerationContext context, float w, float h, float cut, float y0, float y1, Color color)
        {
            float Left(float y) => y < cut ? cut - y : 0;
            float Right(float y) => y > h - cut ? w - (y - (h - cut)) : w;
            var mesh = context.Allocate(4, 6);
            mesh.SetNextVertex(Vert(new Vector2(Left(y0), y0), 0, color));
            mesh.SetNextVertex(Vert(new Vector2(Right(y0), y0), 0, color));
            mesh.SetNextVertex(Vert(new Vector2(Right(y1), y1), 0, color));
            mesh.SetNextVertex(Vert(new Vector2(Left(y1), y1), 0, color));
            mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(2);
            mesh.SetNextIndex(0); mesh.SetNextIndex(2); mesh.SetNextIndex(3);
        }

        static Vertex Vert(Vector2 p, float y0, Color color) =>
            new Vertex { position = new Vector3(p.x, p.y + y0, Vertex.nearZ), tint = color };

        // A ring and ten streaks flying out from `at` (panel coordinates).
        static void Burst(VisualElement e, Vector2 at)
        {
            var host = e.panel?.visualTree;
            if (host == null) return;
            var fx = new BurstFx(at);
            host.Add(fx);
        }

        sealed class BurstFx : VisualElement
        {
            const float Life = .38f;
            readonly float start = Time.unscaledTime;
            readonly float[] angles = new float[10], reach = new float[10];

            public BurstFx(Vector2 at)
            {
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.left = at.x;
                style.top = at.y;
                style.width = 0;
                style.height = 0;
                for (int i = 0; i < angles.Length; i++)
                {
                    angles[i] = i / 10f * Mathf.PI * 2 + Random.Range(-.25f, .25f);
                    reach[i] = Random.Range(50f, 86f);
                }
                generateVisualContent += Paint;
                schedule.Execute(() =>
                {
                    if (Time.unscaledTime - start >= Life) RemoveFromHierarchy();
                    else MarkDirtyRepaint();
                }).Every(16);
            }

            void Paint(MeshGenerationContext context)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - start) / Life);
                float ease = 1 - Mathf.Pow(1 - t, 3);
                var painter = context.painter2D;
                painter.lineCap = LineCap.Round;
                float ring = 60f * Mathf.Min(1, t / .84f);
                if (t < .84f)
                {
                    painter.lineWidth = 2f;
                    painter.strokeColor = new Color(1f, .886f, .627f, .95f * (1 - t / .84f));
                    painter.BeginPath();
                    painter.Arc(Vector2.zero, ring, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
                    painter.Stroke();
                }
                painter.lineWidth = 3f;
                for (int i = 0; i < angles.Length; i++)
                {
                    var dir = new Vector2(Mathf.Cos(angles[i]), Mathf.Sin(angles[i]));
                    var head = dir * reach[i] * ease;
                    float length = Mathf.Lerp(16f, 2f, ease);
                    var color = Sparks[i % Sparks.Length];
                    color.a = 1 - .4f * t;
                    painter.strokeColor = color;
                    painter.BeginPath();
                    painter.MoveTo(head - dir * length / 2);
                    painter.LineTo(head + dir * length / 2);
                    painter.Stroke();
                }
            }
        }
    }
}
