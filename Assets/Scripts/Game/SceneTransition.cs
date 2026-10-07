using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // The scene transition (R84, Docs/Architecture/UI.md §15, design "A 광택 체스판 +
    // C 움직임"): a glossy lit chessboard closes over the screen through a hole
    // the shape of a king that shrinks onto a point (a little bounce before it
    // shuts), a walnut card says where the game goes next while the next screen
    // gets ready behind it, and the board opens again through a knight-shaped hole
    // that grows from a point on the new screen. It lives across scene loads.
    //
    // Points are fractions of the screen (0..1 from the top left), so callers in
    // any panel or camera can say where to close and open.
    [DisallowMultipleComponent]
    public sealed class SceneTransition : MonoBehaviour
    {
        // Over every menu and the settings window (200), under the opening logo (300).
        const float SortingOrder = 250f;
        // Seconds. The hole's scale steps follow the design's keyframes.
        static readonly Vector2[] CloseSteps = { new Vector2(0f, 60f), new Vector2(.103f, 14f), new Vector2(.204f, 5f), new Vector2(.298f, 2.2f), new Vector2(.4f, 1.1f), new Vector2(.458f, 1.22f), new Vector2(.516f, 1.1f), new Vector2(.631f, 0f) };
        static readonly Vector2[] OpenSteps = { new Vector2(0f, 0f), new Vector2(.115f, 1.1f), new Vector2(.173f, 1.22f), new Vector2(.23f, 1.1f), new Vector2(.331f, 2.2f), new Vector2(.425f, 5f), new Vector2(.526f, 14f), new Vector2(.634f, 60f) };
        const float CardIn = .13f, CardOut = .1f, BarSeconds = .62f, BarFinish = .12f, MinCovered = .78f, SweepSeconds = .72f, GiveUpAfter = 20f;
        // The design's keyframe for the last step of the close (it speeds up into the point).
        static readonly Vector4 CloseEase = new Vector4(.6f, 0f, .9f, .4f), SweepEase = new Vector4(.5f, 0f, .3f, 1f);

        enum Phase { Idle, Closing, Covered, Opening }

        public static bool Busy => current != null && current.phase != Phase.Idle;
        static SceneTransition current;

        PanelSettings ownedPanel;
        VisualElement screen, cover, edge, dim, card, barFill;
        Label title;
        Texture2D board, sheen;
        Phase phase = Phase.Idle;
        float phaseAt, readyAt = -1;
        Vector2 closeAt, openAt;
        Action whileCovered, done;
        Func<bool> ready;
        Func<Vector2> openPoint;
        // What the cover draws this frame: the hole's shape, centre and scale.
        Vector2[] holeShape;
        Vector2 holeCentre;
        float holeScale = -1, sweep = -1;

        // Covers the screen closing on `closeAt`, calls `whileCovered` once it is
        // shut, waits for `ready` (and at least a moment, for the card), then opens
        // on `openAt()`. A run already in progress keeps going and this one is dropped.
        public static void Run(string nextScreen, Vector2 closeAt, Action whileCovered, Func<bool> ready, Func<Vector2> openAt, Action done = null)
        {
            if (Busy) return;
            if (current == null)
            {
                var host = new GameObject("Scene Transition");
                DontDestroyOnLoad(host);
                current = host.AddComponent<SceneTransition>();
                current.Build();
            }
            current.Begin(nextScreen, closeAt, whileCovered, ready, openAt, done);
        }

        void Build()
        {
            var root = RuntimePanels.Create(gameObject, Resources.Load<VisualTreeAsset>("TransitionHud"),
                                            Resources.Load<ThemeStyleSheet>("NetworkTheme"), null,
                                            new Vector2Int(1280, 720), out ownedPanel);
            if (ownedPanel != null) ownedPanel.sortingOrder = SortingOrder;
            IntroFlowStyle.Apply(root);
            if (root != null) root.pickingMode = PickingMode.Ignore;
            screen = root?.Q<VisualElement>("tr-screen");
            cover = root?.Q<VisualElement>("tr-cover");
            edge = root?.Q<VisualElement>("tr-edge");
            dim = root?.Q<VisualElement>("tr-dim");
            card = root?.Q<VisualElement>("tr-card");
            barFill = root?.Q<VisualElement>("tr-bar-fill");
            title = root?.Q<Label>("tr-title");
            if (title != null) RuntimePanels.Display(title);
            board = StreamingArt.Picture(StreamingArt.TransitionBoard);
            sheen = Sheen();
            if (cover != null) cover.generateVisualContent += PaintCover;
            if (edge != null) edge.generateVisualContent += PaintEdge;
            if (card != null) card.generateVisualContent += PaintCard;
            var crown = root?.Q<VisualElement>("tr-crown");
            if (crown != null) crown.generateVisualContent += PaintCrown;
            if (dim != null) dim.style.backgroundImage = new StyleBackground(MenuArt.RadialTexture());
            if (barFill != null) barFill.style.backgroundImage = new StyleBackground(MenuArt.Line(new Color(.851f, .682f, .384f), new Color(1f, .878f, .478f)));
        }

        void Begin(string nextScreen, Vector2 from, Action covered, Func<bool> isReady, Func<Vector2> to, Action finished)
        {
            closeAt = from; whileCovered = covered; ready = isReady; openPoint = to; done = finished;
            if (title != null) title.text = nextScreen ?? "";
            phase = Phase.Closing;
            phaseAt = Time.unscaledTime;
            readyAt = -1;
            sweep = -1;
            // Everything under it stops taking clicks until it is open again.
            if (screen != null) screen.pickingMode = PickingMode.Position;
            Draw();
        }

        void Update()
        {
            if (phase == Phase.Idle) return;
            float t = Time.unscaledTime - phaseAt;
            switch (phase)
            {
                case Phase.Closing:
                    if (t >= CloseSteps[CloseSteps.Length - 1].x)
                    {
                        phase = Phase.Covered;
                        phaseAt = Time.unscaledTime;
                        try { whileCovered?.Invoke(); }
                        catch (Exception e) { Debug.LogException(e); }
                    }
                    break;
                case Phase.Covered:
                    bool isReady;
                    try { isReady = ready == null || ready(); }
                    catch (Exception e) { Debug.LogException(e); isReady = true; }
                    if (t >= GiveUpAfter) isReady = true;
                    if (isReady && readyAt < 0 && t >= BarSeconds * .5f) readyAt = Time.unscaledTime;
                    if (readyAt >= 0 && t >= MinCovered && Time.unscaledTime - readyAt >= BarFinish + CardOut)
                    {
                        Vector2 to = closeAt;
                        try { if (openPoint != null) to = openPoint(); }
                        catch (Exception e) { Debug.LogException(e); }
                        openAt = to;
                        phase = Phase.Opening;
                        phaseAt = Time.unscaledTime;
                    }
                    break;
                case Phase.Opening:
                    if (t >= OpenSteps[OpenSteps.Length - 1].x)
                    {
                        phase = Phase.Idle;
                        if (screen != null) screen.pickingMode = PickingMode.Ignore;
                        holeScale = -1;
                        var callback = done;
                        done = null; whileCovered = null; ready = null; openPoint = null;
                        try { callback?.Invoke(); }
                        catch (Exception e) { Debug.LogException(e); }
                    }
                    break;
            }
            Draw();
        }

        void Draw()
        {
            float t = Time.unscaledTime - phaseAt;
            float cardShown = 0f, bar = 0f;
            sweep = -1;
            switch (phase)
            {
                case Phase.Closing:
                    holeShape = ChessOutlines.King;
                    holeCentre = closeAt;
                    holeScale = Steps(CloseSteps, t, CloseEase);
                    break;
                case Phase.Covered:
                    holeScale = 0f;
                    cardShown = Mathf.Clamp01(t / CardIn);
                    bar = Mathf.Min(.9f, t / BarSeconds * .9f);
                    if (readyAt >= 0)
                    {
                        float since = Time.unscaledTime - readyAt;
                        bar = Mathf.Lerp(bar, 1f, Mathf.Clamp01(since / BarFinish));
                        if (since > BarFinish) cardShown = Mathf.Min(cardShown, 1f - Mathf.Clamp01((since - BarFinish) / CardOut));
                    }
                    sweep = Bezier(SweepEase, Mathf.Clamp01(t / SweepSeconds));
                    break;
                case Phase.Opening:
                    holeShape = ChessOutlines.Knight;
                    holeCentre = openAt;
                    holeScale = Steps(OpenSteps, t, Vector4.zero);
                    break;
                default:
                    holeScale = -1;
                    break;
            }
            if (card != null)
            {
                card.style.opacity = cardShown;
                card.style.translate = new Translate(new Length(-190f), new Length(-108f + 10f * (1f - cardShown)));
            }
            if (dim != null) dim.style.opacity = cardShown;
            if (barFill != null) barFill.style.scale = new Scale(new Vector3(Mathf.Clamp01(bar), 1f, 1f));
            cover?.MarkDirtyRepaint();
            edge?.MarkDirtyRepaint();
        }

        // Scale at time t through the steps (straight between them; the last close
        // step eased as in the design).
        static float Steps(Vector2[] steps, float t, Vector4 lastEase)
        {
            if (t <= steps[0].x) return steps[0].y;
            for (int i = 1; i < steps.Length; i++)
            {
                if (t > steps[i].x) continue;
                float u = (t - steps[i - 1].x) / Mathf.Max(1e-5f, steps[i].x - steps[i - 1].x);
                if (i == steps.Length - 1 && lastEase != Vector4.zero) u = Bezier(lastEase, u);
                return Mathf.Lerp(steps[i - 1].y, steps[i].y, u);
            }
            return steps[steps.Length - 1].y;
        }

        // CSS cubic-bezier(x1, y1, x2, y2) at progress x.
        static float Bezier(Vector4 e, float x)
        {
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 24; i++)
            {
                float m = (lo + hi) * .5f;
                float bx = 3f * e.x * m * (1 - m) * (1 - m) + 3f * e.z * m * m * (1 - m) + m * m * m;
                if (bx < x) lo = m; else hi = m;
            }
            float u = (lo + hi) * .5f;
            return 3f * e.y * u * (1 - u) * (1 - u) + 3f * e.w * u * u * (1 - u) + u * u * u;
        }

        // ---------- the cover: the board with a hole ----------

        // The hole in this element's pixels, or null when there is none.
        List<Vector2> Hole(float w, float h)
        {
            if (holeShape == null || holeScale <= 0f) return null;
            float k = holeScale * h / 720f;
            var centre = new Vector2(holeCentre.x * w, holeCentre.y * h);
            var points = new List<Vector2>(holeShape.Length);
            foreach (var q in holeShape) points.Add(centre + (q - ChessOutlines.Anchor) * k);
            return points;
        }

        void PaintCover(MeshGenerationContext context)
        {
            if (phase == Phase.Idle || holeScale < 0f) return;
            float w = cover.layout.width, h = cover.layout.height;
            if (float.IsNaN(w) || w <= 0 || h <= 0) return;
            var hole = Hole(w, h);
            // Wide open: nothing of the board left on screen.
            if (hole != null && Covers(hole, w, h)) return;
            var points = new List<Vector2>();
            var triangles = new List<int>();
            if (hole == null) AddRect(points, triangles, 0, 0, w, h);
            else Ring(holeShape, new Vector2(holeCentre.x * w, holeCentre.y * h), holeScale * h / 720f, w, h, points, triangles);
            if (board != null) Mesh(context, points, triangles, board, q => BoardUv(q, w, h, board));
            else Mesh(context, points, triangles, null, q => Vector2.zero, new Color32(70, 42, 22, 255));
            if (sweep >= 0f && sheen != null)
            {
                // A wide light band at 18 degrees sweeping left to right (-700 → 1700 design px).
                float s = h / 720f, x = Mathf.Lerp(-700f, 1700f, sweep) * s;
                float cos = Mathf.Cos(18f * Mathf.Deg2Rad), sin = Mathf.Sin(18f * Mathf.Deg2Rad);
                Mesh(context, points, triangles, sheen, q =>
                {
                    var d = q - new Vector2(x + 130f * s, 360f * s);
                    return new Vector2((d.x * cos + d.y * sin + 130f * s) / (260f * s), .5f);
                });
            }
        }

        void PaintEdge(MeshGenerationContext context)
        {
            if (phase == Phase.Idle) return;
            float w = edge.layout.width, h = edge.layout.height;
            if (float.IsNaN(w) || w <= 0 || h <= 0) return;
            var hole = Hole(w, h);
            if (hole == null || Covers(hole, w, h)) return;
            var p = context.painter2D;
            p.lineJoin = LineJoin.Round;
            float s = h / 720f;
            void Stroke(float width, Color c)
            {
                p.lineWidth = width * s;
                p.strokeColor = c;
                p.BeginPath();
                p.MoveTo(hole[0]);
                for (int i = 1; i < hole.Count; i++) p.LineTo(hole[i]);
                p.ClosePath();
                p.Stroke();
            }
            Stroke(9f, new Color(1f, .788f, .239f, IntroFlowStyle.Light(.3f)));
            Stroke(3f, new Color(.949f, .816f, .541f, 1f));
        }

        // The board scaled to cover the element, cropped at the edges.
        static Vector2 BoardUv(Vector2 q, float w, float h, Texture2D picture)
        {
            float scale = Mathf.Max(w / picture.width, h / picture.height);
            float pw = picture.width * scale, ph = picture.height * scale;
            float u = (q.x - (w - pw) * .5f) / pw, v = (q.y - (h - ph) * .5f) / ph;
            return new Vector2(u, 1f - v);
        }

        static bool Covers(List<Vector2> hole, float w, float h)
        {
            // Every corner of the screen inside the hole: the board is all outside.
            return Inside(hole, new Vector2(0, 0)) && Inside(hole, new Vector2(w, 0)) && Inside(hole, new Vector2(w, h)) && Inside(hole, new Vector2(0, h))
                   && !AnyInside(hole, w, h);
        }

        // Any hole corner on screen means a part of the board may still show.
        static bool AnyInside(List<Vector2> hole, float w, float h)
        {
            foreach (var q in hole) if (q.x > 0 && q.x < w && q.y > 0 && q.y < h) return true;
            return false;
        }

        static void Mesh(MeshGenerationContext context, List<Vector2> points, List<int> triangles, Texture2D texture, Func<Vector2, Vector2> uv, Color32? tint = null)
        {
            if (triangles.Count < 3 || points.Count > ushort.MaxValue) return;
            var data = context.Allocate(points.Count, triangles.Count, texture);
            var vertices = new Vertex[points.Count];
            var colour = tint ?? new Color32(255, 255, 255, 255);
            for (int i = 0; i < points.Count; i++)
                vertices[i] = new Vertex { position = new Vector3(points[i].x, points[i].y, Vertex.nearZ), tint = colour, uv = uv(points[i]) };
            var indices = new ushort[triangles.Count];
            for (int i = 0; i < triangles.Count; i++) indices[i] = (ushort)triangles[i];
            data.SetAllVertices(vertices);
            data.SetAllIndices(indices);
        }

        // The board around a hole of `shape` scaled by `k` about its anchor at
        // `centre`: a square around the shape minus the shape, cut into triangles
        // once per shape and only moved and scaled after that, plus up to four
        // strips filling the rest of the screen outside that square.
        static readonly Rect Around = new Rect(-150f, -150f, 400f, 500f);
        static readonly Dictionary<Vector2[], KeyValuePair<Vector2[], int[]>> rings = new Dictionary<Vector2[], KeyValuePair<Vector2[], int[]>>();

        static void Ring(Vector2[] shape, Vector2 centre, float k, float w, float h, List<Vector2> points, List<int> triangles)
        {
            if (!rings.TryGetValue(shape, out var ring)) rings[shape] = ring = Cut(shape);
            foreach (var q in ring.Key) points.Add(centre + (q - ChessOutlines.Anchor) * k);
            triangles.AddRange(ring.Value);
            float x0 = centre.x + (Around.xMin - ChessOutlines.Anchor.x) * k, x1 = centre.x + (Around.xMax - ChessOutlines.Anchor.x) * k;
            float y0 = centre.y + (Around.yMin - ChessOutlines.Anchor.y) * k, y1 = centre.y + (Around.yMax - ChessOutlines.Anchor.y) * k;
            float cy0 = Mathf.Clamp(y0, 0, h), cy1 = Mathf.Clamp(y1, 0, h), cx0 = Mathf.Clamp(x0, 0, w), cx1 = Mathf.Clamp(x1, 0, w);
            AddRect(points, triangles, 0, 0, w, cy0);
            AddRect(points, triangles, 0, cy1, w, h - cy1);
            AddRect(points, triangles, 0, cy0, cx0, cy1 - cy0);
            AddRect(points, triangles, cx1, cy0, w - cx1, cy1 - cy0);
        }

        static void AddRect(List<Vector2> points, List<int> triangles, float x, float y, float w, float h)
        {
            if (w <= 0 || h <= 0) return;
            int i = points.Count;
            points.Add(new Vector2(x, y)); points.Add(new Vector2(x + w, y)); points.Add(new Vector2(x + w, y + h)); points.Add(new Vector2(x, y + h));
            triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
            triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3);
        }

        // The square around the shape minus the shape, as triangles: the shape is
        // bridged to the square's top right corner (nothing of the shape lies right
        // of its rightmost point, so the bridge crosses no edge) and the one
        // outline is cut into ears.
        static KeyValuePair<Vector2[], int[]> Cut(Vector2[] shape)
        {
            // Clockwise on screen (y down): top left, top right, bottom right, bottom left.
            var outer = new List<Vector2> { new Vector2(Around.xMin, Around.yMin), new Vector2(Around.xMax, Around.yMin), new Vector2(Around.xMax, Around.yMax), new Vector2(Around.xMin, Around.yMax) };
            var inner = new List<Vector2>(shape);
            if (Area(inner) * Area(outer) > 0) inner.Reverse();
            int m = 0;
            for (int i = 1; i < inner.Count; i++) if (inner[i].x > inner[m].x) m = i;
            var points = new List<Vector2> { outer[0], outer[1] };
            for (int i = 0; i <= inner.Count; i++) points.Add(inner[(m + i) % inner.Count]);
            points.Add(outer[1]);
            points.Add(outer[2]);
            points.Add(outer[3]);
            return new KeyValuePair<Vector2[], int[]>(points.ToArray(), EarClip(points).ToArray());
        }

        static float Area(List<Vector2> v)
        {
            float a = 0;
            for (int i = 0, j = v.Count - 1; i < v.Count; j = i++) a += (v[j].x * v[i].y) - (v[i].x * v[j].y);
            return a * .5f;
        }

        static float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

        static List<int> EarClip(List<Vector2> p)
        {
            var result = new List<int>();
            var index = new List<int>(p.Count);
            for (int i = 0; i < p.Count; i++) index.Add(i);
            float sign = Area(p) >= 0 ? 1f : -1f;
            int guard = p.Count * p.Count;
            while (index.Count > 3 && guard-- > 0)
            {
                bool cut = false;
                for (int i = 0; i < index.Count; i++)
                {
                    int ia = index[(i + index.Count - 1) % index.Count], ib = index[i], ic = index[(i + 1) % index.Count];
                    Vector2 a = p[ia], b = p[ib], c = p[ic];
                    float turn = Cross(a, b, c) * sign;
                    if (turn <= 1e-6f) continue;
                    bool blocked = false;
                    foreach (int j in index)
                    {
                        if (j == ia || j == ib || j == ic) continue;
                        var q = p[j];
                        if (q == a || q == b || q == c) continue;
                        if (InTriangle(q, a, b, c)) { blocked = true; break; }
                    }
                    if (blocked) continue;
                    Add(result, p, ia, ib, ic);
                    index.RemoveAt(i);
                    cut = true;
                    break;
                }
                // A degenerate leftover (a zero-width sliver along the bridge): drop one corner.
                if (!cut)
                {
                    int worst = 0;
                    float least = float.MaxValue;
                    for (int i = 0; i < index.Count; i++)
                    {
                        float turn = Mathf.Abs(Cross(p[index[(i + index.Count - 1) % index.Count]], p[index[i]], p[index[(i + 1) % index.Count]]));
                        if (turn < least) { least = turn; worst = i; }
                    }
                    index.RemoveAt(worst);
                }
            }
            if (index.Count == 3) Add(result, p, index[0], index[1], index[2]);
            return result;
        }

        // Every triangle wound clockwise on screen.
        static void Add(List<int> result, List<Vector2> p, int a, int b, int c)
        {
            if (Cross(p[a], p[b], p[c]) < 0) { int t = b; b = c; c = t; }
            result.Add(a); result.Add(b); result.Add(c);
        }

        // Strictly inside: a point on an edge (the shape's straight runs, the
        // bridge's doubled corners) does not stop an ear.
        static bool InTriangle(Vector2 q, Vector2 a, Vector2 b, Vector2 c)
        {
            const float e = 1e-4f;
            float d1 = Cross(a, b, q), d2 = Cross(b, c, q), d3 = Cross(c, a, q);
            return (d1 > e && d2 > e && d3 > e) || (d1 < -e && d2 < -e && d3 < -e);
        }

        static bool Inside(List<Vector2> v, Vector2 q)
        {
            bool inside = false;
            for (int i = 0, j = v.Count - 1; i < v.Count; j = i++)
                if ((v[i].y > q.y) != (v[j].y > q.y) && q.x < (v[j].x - v[i].x) * (q.y - v[i].y) / (v[j].y - v[i].y) + v[i].x)
                    inside = !inside;
            return inside;
        }

        // ---------- the card ----------

        // A walnut plate with clipped corners, a gold hairline, a dark band and a
        // fainter gold line inside it.
        void PaintCard(MeshGenerationContext context)
        {
            float w = card.layout.width, h = card.layout.height;
            if (float.IsNaN(w) || w <= 0) return;
            var p = context.painter2D;
            var wood = new Gradient();
            wood.SetKeys(new[] { new GradientColorKey(new Color(.306f, .196f, .114f), 0f), new GradientColorKey(new Color(.165f, .098f, .055f), 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            p.fillGradient = FillGradient.MakeLinearGradient(wood, new Vector2(0, 0), new Vector2(0, h), AddressMode.Clamp);
            Octagon(p, 0, 0, w, h, 14f);
            p.Fill();
            p.fillColor = new Color(.165f, .098f, .055f);
            p.BeginPath();
            OctagonPath(p, 1f, 1f, w - 2f, h - 2f, 13.6f);
            OctagonPath(p, 8f, 8f, w - 16f, h - 16f, 10.7f);
            p.Fill(FillRule.OddEven);
            p.lineWidth = 1f;
            p.strokeColor = new Color(.851f, .682f, .384f, IntroFlowStyle.Light(.55f));
            Octagon(p, .5f, .5f, w - 1f, h - 1f, 13.8f);
            p.Stroke();
            p.strokeColor = new Color(.851f, .682f, .384f, IntroFlowStyle.Light(.25f));
            Octagon(p, 8.5f, 8.5f, w - 17f, h - 17f, 10.5f);
            p.Stroke();
        }

        static void Octagon(Painter2D p, float x, float y, float w, float h, float c)
        {
            p.BeginPath();
            OctagonPath(p, x, y, w, h, c);
        }

        static void OctagonPath(Painter2D p, float x, float y, float w, float h, float c)
        {
            p.MoveTo(new Vector2(x + c, y));
            p.LineTo(new Vector2(x + w - c, y));
            p.LineTo(new Vector2(x + w, y + c));
            p.LineTo(new Vector2(x + w, y + h - c));
            p.LineTo(new Vector2(x + w - c, y + h));
            p.LineTo(new Vector2(x + c, y + h));
            p.LineTo(new Vector2(x, y + h - c));
            p.LineTo(new Vector2(x, y + c));
            p.ClosePath();
        }

        // The card's crown (44 × 30).
        static void PaintCrown(MeshGenerationContext context)
        {
            var p = context.painter2D;
            p.fillColor = new Color(1f, .788f, .239f);
            p.strokeColor = new Color(1f, .906f, .627f);
            p.lineWidth = 1.5f;
            p.lineJoin = LineJoin.Round;
            p.BeginPath();
            p.MoveTo(new Vector2(4, 26)); p.LineTo(new Vector2(6, 8)); p.LineTo(new Vector2(15, 16)); p.LineTo(new Vector2(22, 3));
            p.LineTo(new Vector2(29, 16)); p.LineTo(new Vector2(38, 8)); p.LineTo(new Vector2(40, 26));
            p.ClosePath();
            p.Fill();
            p.Stroke();
            p.fillColor = new Color(1f, .878f, .478f);
            foreach (var c in new[] { new Vector2(6, 7), new Vector2(22, 3), new Vector2(38, 7) })
            {
                p.BeginPath();
                p.Arc(c, 2.6f, 0f, 360f);
                p.Fill();
            }
        }

        // The light band's profile across its width: clear, a warm .3 in the middle, clear.
        static Texture2D Sheen()
        {
            const int w = 64;
            var texture = new Texture2D(w, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Transition Sheen" };
            var px = new Color[w];
            for (int x = 0; x < w; x++)
            {
                float u = x / (w - 1f);
                px[x] = new Color(1f, .925f, .784f, IntroFlowStyle.Light(.3f * (1f - Mathf.Abs(u * 2f - 1f))));
            }
            px[0].a = px[w - 1].a = 0f;
            texture.SetPixels(px);
            texture.Apply(false, true);
            return texture;
        }

        void OnDestroy()
        {
            if (current == this) current = null;
            if (sheen != null) Destroy(sheen);
            if (ownedPanel != null) Destroy(ownedPanel);
        }
    }

    // The two hole shapes from the design (a 100 × 200 box, drawn standing on its
    // base): the king that closes and the knight that opens. Anchor is the point
    // that stays put while the hole grows or shrinks.
    public static class ChessOutlines
    {
        public static readonly Vector2 Anchor = new Vector2(50f, 150f);
        public static readonly Vector2[] King = Parse("M45 0 H55 V9 H63 V18 H55 V29 Q58 29 58 32 L69 35 Q72 37 70 41 L63 56 Q72 57 72 61 Q72 66 65 67 Q60 72 61 84 L67 128 Q69 143 77 149 Q83 150 83 155 Q83 160 77 161 L86 176 Q91 179 91 186 L91 196 H9 L9 186 Q9 179 14 176 L23 161 Q17 160 17 155 Q17 150 23 149 Q31 143 33 128 L39 84 Q40 72 35 67 Q28 66 28 61 Q28 57 37 56 L30 41 Q28 37 31 35 L42 32 Q42 29 45 29 V18 H37 V9 H45 Z");
        public static readonly Vector2[] Knight = Parse("M9 196 L9 186 Q9 179 15 177 L19 176 L19 165 Q19 161 24 161 L30 161 L34 117 Q22 108 21 96 Q20 88 25 84 L40 54 L43 31 Q44 25 48 29 L55 40 Q80 47 82 86 L81 161 L81 176 L85 177 Q91 179 91 186 L91 196 Z");

        // Absolute M, L, H, V, Q and Z only, curves cut into eight straight steps.
        static Vector2[] Parse(string path)
        {
            var points = new List<Vector2>();
            var tokens = path.Replace(",", " ").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var at = Vector2.zero;
            char command = 'M';
            int i = 0;
            float Next() => float.Parse(tokens[i++].TrimStart('M', 'L', 'H', 'V', 'Q', 'Z'), System.Globalization.CultureInfo.InvariantCulture);
            while (i < tokens.Length)
            {
                string token = tokens[i];
                if (char.IsLetter(token[0]))
                {
                    command = token[0];
                    if (token.Length == 1) { i++; if (command == 'Z') continue; }
                    else tokens[i] = token.Substring(1);
                    if (command == 'Z') continue;
                }
                switch (command)
                {
                    case 'M': case 'L': at = new Vector2(Next(), Next()); points.Add(at); break;
                    case 'H': at = new Vector2(Next(), at.y); points.Add(at); break;
                    case 'V': at = new Vector2(at.x, Next()); points.Add(at); break;
                    case 'Q':
                        var c = new Vector2(Next(), Next());
                        var b = new Vector2(Next(), Next());
                        for (int s = 1; s <= 8; s++)
                        {
                            float t = s / 8f;
                            points.Add((1 - t) * (1 - t) * at + 2 * (1 - t) * t * c + t * t * b);
                        }
                        at = b;
                        break;
                    default: i++; break;
                }
            }
            // The path closes on its start: drop a repeated last point.
            if (points.Count > 1 && points[points.Count - 1] == points[0]) points.RemoveAt(points.Count - 1);
            return points.ToArray();
        }
    }
}
