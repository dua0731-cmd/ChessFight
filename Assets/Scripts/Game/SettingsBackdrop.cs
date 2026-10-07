using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ChessFight.Game
{
    // The settings screen's still background (R77, design sample "체스판 배경"):
    // a maple and walnut chessboard lying away into the dark under a warm lamp,
    // a mahogany frame with a brass line, a large black king on the right edge,
    // an ivory knight bottom left and a soft-focus queen, rook and pawns behind,
    // each piece shaded with a gradient lit from the upper left and a thin rim of
    // light on its far side. Drawn once in code at 1280 × 720 and kept.
    public static class SettingsBackdrop
    {
        const int W = 1280, H = 720;
        static Texture2D cached;
        static Task<Color[]> drawing;

        // Drawing takes about a second, so it runs on a worker thread from the
        // start of the game (GameSettings.Load) and is ready long before Esc.
        public static void Prewarm()
        {
            if (cached == null && drawing == null) drawing = Task.Run(() => Build());
        }

        // The picture, or null while it is still being drawn.
        public static Texture2D TryGet()
        {
            if (cached != null) return cached;
            Prewarm();
            if (!drawing.IsCompleted) return null;
            if (drawing.IsFaulted)
            {
                Debug.LogException(drawing.Exception);
                drawing = null;
                return null;
            }
            cached = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                name = "Settings Backdrop", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            cached.SetPixels(drawing.Result);
            cached.Apply(false, true);
            drawing = null;
            return cached;
        }

        // ---------- shapes in a piece's own 100 × 200 box ----------

        interface IShape { bool Inside(Vector2 p); }

        sealed class Circle : IShape
        {
            readonly Vector2 c; readonly float r2;
            public Circle(float x, float y, float r) { c = new Vector2(x, y); r2 = r * r; }
            public bool Inside(Vector2 p) => (p - c).sqrMagnitude <= r2;
        }

        sealed class Box : IShape
        {
            readonly float x0, y0, x1, y1, r;
            public Box(float x, float y, float w, float h, float radius) { x0 = x; y0 = y; x1 = x + w; y1 = y + h; r = radius; }
            public bool Inside(Vector2 p)
            {
                if (p.x < x0 || p.x > x1 || p.y < y0 || p.y > y1) return false;
                float cx = Mathf.Clamp(p.x, x0 + r, x1 - r), cy = Mathf.Clamp(p.y, y0 + r, y1 - r);
                return (p.x - cx) * (p.x - cx) + (p.y - cy) * (p.y - cy) <= r * r;
            }
        }

        sealed class Poly : IShape
        {
            readonly Vector2[] v;
            public Poly(params Vector2[] points) { v = points; }
            public bool Inside(Vector2 p)
            {
                bool inside = false;
                for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
                    if ((v[i].y > p.y) != (v[j].y > p.y) && p.x < (v[j].x - v[i].x) * (p.y - v[i].y) / (v[j].y - v[i].y) + v[i].x)
                        inside = !inside;
                return inside;
            }
        }

        // A path from straight runs and quadratic curves: "L x y" or "Q cx cy x y".
        static Poly Path(float x, float y, params float[][] steps)
        {
            var points = new List<Vector2> { new Vector2(x, y) };
            foreach (var s in steps)
            {
                if (s.Length == 2) { points.Add(new Vector2(s[0], s[1])); continue; }
                var a = points[points.Count - 1];
                var c = new Vector2(s[0], s[1]);
                var b = new Vector2(s[2], s[3]);
                for (int i = 1; i <= 8; i++)
                {
                    float t = i / 8f;
                    points.Add((1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b);
                }
            }
            return new Poly(points.ToArray());
        }

        static float[] L(float x, float y) => new[] { x, y };
        static float[] Q(float cx, float cy, float x, float y) => new[] { cx, cy, x, y };
        static Vector2 V(float x, float y) => new Vector2(x, y);

        static IShape[] Bases => new IShape[] { new Box(18, 160, 64, 14, 4), new Box(12, 174, 76, 18, 5) };

        static IShape[] With(IShape[] top) { var list = new List<IShape>(top); list.AddRange(Bases); return list.ToArray(); }

        static IShape[] Pawn => With(new IShape[]
        {
            new Circle(50, 60, 20), new Box(34, 80, 32, 8, 3), Path(38, 88, Q(50, 92, 62, 88), L(72, 160), L(28, 160)),
        });

        static IShape[] Rook => With(new IShape[]
        {
            new Poly(V(26, 20), V(38, 20), V(38, 32), V(44, 32), V(44, 20), V(56, 20), V(56, 32), V(62, 32), V(62, 20), V(74, 20), V(74, 52), V(26, 52)),
            new Poly(V(32, 52), V(68, 52), V(72, 160), V(28, 160)),
        });

        static IShape[] Queen => With(new IShape[]
        {
            new Circle(34, 28, 4), new Circle(50, 20, 4.5f), new Circle(66, 28, 4),
            new Poly(V(28, 54), V(34, 30), V(42, 46), V(50, 22), V(58, 46), V(66, 30), V(72, 54)),
            new Box(32, 54, 36, 8, 3), Path(36, 62, Q(50, 68, 64, 62), L(72, 160), L(28, 160)),
        });

        static IShape[] King => With(new IShape[]
        {
            new Box(45, 2, 10, 34, 2), new Box(35, 12, 30, 9, 2),
            Path(30, 42, Q(50, 30, 70, 42), L(66, 72), Q(50, 78, 34, 72)),
            new Box(31, 72, 38, 8, 3), Path(36, 80, Q(50, 84, 64, 80), L(72, 160), L(28, 160)),
        });

        static IShape[] Knight => With(new IShape[]
        {
            Path(30, 160, L(34, 112), Q(22, 102, 24, 86), L(42, 46), L(46, 24), L(56, 38), Q(80, 46, 82, 82), L(84, 160)),
        });

        // ---------- materials ----------

        struct Material
        {
            public Color[] Stops; public float[] At;
            public Vector2 Light;
            public Color RimLeft, RimRight;
        }

        static Color Hex(int rgb, float a = 1f) => new Color((rgb >> 16 & 255) / 255f, (rgb >> 8 & 255) / 255f, (rgb & 255) / 255f, a);

        static readonly Material Lacquer = new Material
        {
            Stops = new[] { Hex(0x8A5C36), Hex(0x4A2E1A), Hex(0x1E120A), Hex(0x080402) }, At = new[] { 0f, .28f, .7f, 1f },
            Light = new Vector2(30, 48), RimLeft = Hex(0xD9AE62, .15f), RimRight = Hex(0xFFD58A, .75f),
        };

        static readonly Material Ivory = new Material
        {
            Stops = new[] { Hex(0xFFF8EA), Hex(0xEBCB98), Hex(0xA97C4C), Hex(0x4A2E1A) }, At = new[] { 0f, .3f, .72f, 1f },
            Light = new Vector2(34, 50), RimLeft = Hex(0xFFFFFF, .5f), RimRight = Hex(0x3A2414, .6f),
        };

        static Color Ramp(Material m, float t)
        {
            t = Mathf.Clamp01(t);
            for (int i = 1; i < m.At.Length; i++)
                if (t <= m.At[i]) return Color.Lerp(m.Stops[i - 1], m.Stops[i], (t - m.At[i - 1]) / (m.At[i] - m.At[i - 1]));
            return m.Stops[m.Stops.Length - 1];
        }

        // ---------- the picture ----------

        static Color[] Build()
        {
            var px = new Color[W * H];
            for (int sy = 0; sy < H; sy++)
                for (int x = 0; x < W; x++)
                    px[Index(x, sy)] = Base(x, sy);

            Board(px);
            // The far half of the board sinks into the dark.
            for (int sy = 0; sy < 330; sy++)
            {
                float a = .9f * (sy < 66 ? 1f : 1f - (sy - 66) / 264f);
                for (int x = 0; x < W; x++) Blend(px, x, sy, Hex(0x120A05), a);
            }
            Halo(px, 1230, 380, 210, 260, new Color(1f, .77f, .43f), .14f);
            Halo(px, 50, 520, 210, 220, new Color(1f, .86f, .63f), .10f);

            Piece(px, Pawn, Ivory, 330, 236, 70, 140, .5f, 2.5f);
            Piece(px, Rook, Lacquer, 610, 226, 76, 152, .45f, 2.5f);
            Piece(px, Pawn, Lacquer, 840, 240, 66, 132, .45f, 2.5f);
            Piece(px, Queen, Ivory, 980, 218, 130, 260, .75f, 1.2f);
            Piece(px, King, Lacquer, 1098, 170, 250, 500, 1f, 0f);
            Piece(px, Knight, Ivory, -46, 300, 200, 400, 1f, 0f, new Vector2(54, 56));

            // Vignette: edges darker, the middle left readable.
            for (int sy = 0; sy < H; sy++)
                for (int x = 0; x < W; x++)
                {
                    float dx = (x - W * .5f) / (W * .75f), dy = (sy - H * .45f) / (H * .7f);
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    Blend(px, x, sy, Color.Lerp(Hex(0x0E0804), Hex(0x080402), d), Mathf.Lerp(.30f, .78f, d));
                }

            return px;
        }

        // Screen rows run top down; a texture's rows bottom up.
        static int Index(int x, int sy) => (H - 1 - sy) * W + x;

        static void Blend(Color[] px, int x, int sy, Color c, float a)
        {
            if (x < 0 || x >= W || sy < 0 || sy >= H || a <= 0) return;
            int i = Index(x, sy);
            px[i] = Color.Lerp(px[i], c, Mathf.Clamp01(a));
            px[i].a = 1f;
        }

        // Dark walnut, a warm lamp from the top left.
        static Color Base(int x, int sy)
        {
            var c = Color.Lerp(Hex(0x24150B), Hex(0x0E0805), sy / (H - 1f));
            float dx = (x - W * .18f) / (W * .7f), dy = sy / (H * .6f);
            float lamp = .20f * (1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / .7f));
            return Color.Lerp(c, new Color(1f, .75f, .35f), lamp);
        }

        static void Halo(Color[] px, float cx, float cy, float rx, float ry, Color c, float strength)
        {
            for (int sy = Mathf.Max(0, (int)(cy - ry)); sy < Mathf.Min(H, (int)(cy + ry)); sy++)
                for (int x = Mathf.Max(0, (int)(cx - rx)); x < Mathf.Min(W, (int)(cx + rx)); x++)
                {
                    float dx = (x - cx) / rx, dy = (sy - cy) / ry;
                    Blend(px, x, sy, c, strength * (1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / .7f)));
                }
        }

        // The board on a floor seen from above: z grows with distance, one square
        // 230 px wide at the bottom edge. 2 × 2 samples a pixel for clean edges.
        static void Board(Color[] px)
        {
            const float horizon = 250f, cx = 640f, near = 470f, square = 230f;
            Color maple = Hex(0xC9A77A), walnut = Hex(0x5A3A22), mahogany = Hex(0x4A1E10), brass = Hex(0xD9AE62);
            for (int sy = (int)horizon + 6; sy < H; sy++)
                for (int x = 0; x < W; x++)
                {
                    Color sum = Color.clear;
                    float covered = 0;
                    for (int s = 0; s < 4; s++)
                    {
                        float fx = x + (s & 1) * .5f + .25f, fy = sy + (s >> 1) * .5f + .25f;
                        float d = fy - horizon, z = near / d, u = (fx - cx) / square * z;
                        float au = Mathf.Abs(u);
                        if (au > 4.3f) continue;
                        Color c;
                        if (au > 4f) c = au < 4.06f ? brass : mahogany;
                        else
                        {
                            int col = Mathf.FloorToInt(u + 4f), row = Mathf.FloorToInt(z + .3f);
                            bool light = ((col + row) & 1) == 0;
                            c = light ? maple : walnut;
                            // A faint grain along each square, different per square.
                            float grain = Mathf.Sin((u * 9f + row * 3.7f + col * 1.3f) * 6f) * .025f;
                            c = new Color(c.r * (1 + grain), c.g * (1 + grain), c.b * (1 + grain));
                        }
                        // The lamp's pool and the far squares darkening.
                        float pu = u - 1.2f, pz = (z - 1.8f) * .8f;
                        float pool = .22f * (1f - Mathf.Clamp01(Mathf.Sqrt(pu * pu + pz * pz) / 2.4f));
                        c = Color.Lerp(c, new Color(1f, .8f, .51f), pool);
                        c *= .8f * Mathf.Lerp(.62f, 1f, Mathf.Clamp01((d - 20f) / 260f));
                        sum += c;
                        covered += Mathf.Clamp01((d - 15f) / 120f);
                    }
                    if (covered <= 0) continue;
                    var color = sum / 4f * (4f / Mathf.Max(1, CountInside(x, sy, horizon, cx, near, square)));
                    Blend(px, x, sy, color, covered / 4f);
                }
        }

        static int CountInside(int x, int sy, float horizon, float cx, float near, float square)
        {
            int n = 0;
            for (int s = 0; s < 4; s++)
            {
                float fx = x + (s & 1) * .5f + .25f, fy = sy + (s >> 1) * .5f + .25f;
                float z = near / (fy - horizon);
                if (Mathf.Abs((fx - cx) / square * z) <= 4.3f) n++;
            }
            return n;
        }

        // One piece into its own layer (shadow, gradient body, rim light), blurred
        // when it stands far back, then laid over the picture.
        static void Piece(Color[] px, IShape[] shapes, Material m, int left, int top, int w, int h,
                          float opacity, float blur, Vector2? eye = null)
        {
            int margin = Mathf.CeilToInt(blur * 3f) + 2;
            int lw = w + margin * 2, lh = h + margin * 2;
            var layer = new Color[lw * lh];
            float sx = 100f / w, sy = 200f / h;
            bool In(Vector2 p) { foreach (var shape in shapes) if (shape.Inside(p)) return true; return false; }

            for (int ly = 0; ly < lh; ly++)
                for (int lx = 0; lx < lw; lx++)
                {
                    var p = new Vector2((lx - margin + .5f) * sx, (ly - margin + .5f) * sy);
                    // The shadow on the board under the base.
                    float ex = (p.x - 50f) / 46f, ey = (p.y - 193f) / 6f;
                    float shadow = ex * ex + ey * ey <= 1f ? .5f : 0f;
                    int hits = 0;
                    for (int s = 0; s < 4; s++)
                        if (In(p + new Vector2(((s & 1) - .5f) * .5f * sx, ((s >> 1) - .5f) * .5f * sy))) hits++;
                    float cover = hits / 4f;
                    var c = Ramp(m, (p - m.Light).magnitude / 150f);
                    if (eye.HasValue && (p - eye.Value).sqrMagnitude < 9f) c = Hex(0x2C1A0E);
                    // Rim: the edge of the silhouette, gold on the right for lacquer,
                    // a white catch-light on the left for ivory.
                    if (cover > 0 && (!In(p + new Vector2(2.2f, 0)) || !In(p - new Vector2(2.2f, 0)) || !In(p - new Vector2(0, 2.2f))))
                    {
                        var rim = Color.Lerp(m.RimLeft, m.RimRight, Mathf.Clamp01(p.x / 100f));
                        c = Color.Lerp(c, new Color(rim.r, rim.g, rim.b), rim.a);
                    }
                    float alpha = Mathf.Max(cover, shadow * (1f - cover));
                    var shaded = cover > 0 ? Color.Lerp(Color.black, c, cover / alpha) : Color.black;
                    layer[ly * lw + lx] = new Color(shaded.r, shaded.g, shaded.b, alpha);
                }

            if (blur > 0) BoxBlur(layer, lw, lh, Mathf.RoundToInt(blur * 1.5f));

            for (int ly = 0; ly < lh; ly++)
                for (int lx = 0; lx < lw; lx++)
                {
                    var c = layer[ly * lw + lx];
                    if (c.a <= 0) continue;
                    Blend(px, left - margin + lx, top - margin + ly, c, c.a * opacity);
                }
        }

        // Two passes of a box blur, premultiplied so edges fade instead of darkening.
        static void BoxBlur(Color[] layer, int w, int h, int r)
        {
            if (r <= 0) return;
            var tmp = new Color[layer.Length];
            for (int i = 0; i < layer.Length; i++) { var c = layer[i]; layer[i] = new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a); }
            for (int pass = 0; pass < 2; pass++)
            {
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        Color s = Color.clear; int n = 0;
                        for (int k = -r; k <= r; k++) { int xx = x + k; if (xx < 0 || xx >= w) continue; s += layer[y * w + xx]; n++; }
                        tmp[y * w + x] = s / n;
                    }
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        Color s = Color.clear; int n = 0;
                        for (int k = -r; k <= r; k++) { int yy = y + k; if (yy < 0 || yy >= h) continue; s += tmp[yy * w + x]; n++; }
                        layer[y * w + x] = s / n;
                    }
            }
            for (int i = 0; i < layer.Length; i++)
            {
                var c = layer[i];
                layer[i] = c.a > 0 ? new Color(c.r / c.a, c.g / c.a, c.b / c.a, c.a) : Color.clear;
            }
        }
    }
}
