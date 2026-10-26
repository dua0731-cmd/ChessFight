using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The annotation stamps themselves (R128, shape C of the mock-up page MfeQTucSW98P3qZxf2V795: the symbol alone, a
    /// thick ink edge and an ink shadow, in the grade's colour): !! gold leaf, ! ivory, !? walnut, ? ebony, ?? red — never
    /// the side colours (cyan, orange). Slammed in like the rest of the skills ("팍!"): it drops from above, freezes
    /// squashed for a few frames while it flashes white, springs back, then holds and lifts away. !! is bigger, throws an
    /// ivory ring and twelve gold rays and shakes; ! an ivory ring and six ticks; ? and ?? land with a dull ring and tilt.
    /// Over the head at a steady size on screen; in Sword Fight it sits on the lower right of the piece token instead
    /// (the token stays up for it, or pops again if it had gone), like "N!!" in a score sheet.
    /// </summary>
    public class SkillGradeStamps : MonoBehaviour
    {
        static readonly Color Ink = new Color32(0x15, 0x1a, 0x33, 0xff);
        static readonly Color Ivory = new Color32(0xF3, 0xE8, 0xCF, 0xff);
        static readonly Color Ray = new Color32(0xFF, 0xD4, 0x5A, 0xff);

        static Color FillOf(SkillGrade g)
        {
            switch (g)
            {
                case SkillGrade.Brilliant: return new Color32(0xE2, 0xB1, 0x3C, 0xff);
                case SkillGrade.Great: return Ivory;
                case SkillGrade.Interesting: return new Color32(0xB8, 0x84, 0x57, 0xff);
                case SkillGrade.Mistake: return new Color32(0x2B, 0x25, 0x22, 0xff);
                case SkillGrade.Blunder: return new Color32(0xC2, 0x41, 0x2F, 0xff);
                default: return new Color32(0x8C, 0x84, 0x78, 0xff);
            }
        }

        static float HoldOf(SkillGrade g) =>
            g == SkillGrade.Brilliant ? 1.5f : g == SkillGrade.Blunder ? 1.3f : g == SkillGrade.Interesting ? 1.2f : 1.15f;

        /// <summary>How long a stamp is up, all told (s).</summary>
        public static float LifeOf(SkillGrade g) => HoldOf(g) + 0.25f;

        class Live
        {
            public RagdollPawn pawn;
            public SkillGrade g;
            public bool sword;
            public float born;
            public Transform glyph, ring, rays;
            public Renderer glyphR, ringR, raysR;
            public MaterialPropertyBlock gb, rb, yb;
            public float aspect;
        }

        readonly Dictionary<RagdollPawn, Live> live = new Dictionary<RagdollPawn, Live>();
        readonly List<RagdollPawn> done = new List<RagdollPawn>();
        readonly Dictionary<string, Texture2D> glyphs = new Dictionary<string, Texture2D>();
        Texture2D ringTex, raysTex, ticksTex;
        Material front, back;
        Mesh quad;
        float clock;
        SwordFightSkillFx swordFx;
        bool swordLooked;

        // ---------------------------------------------------------------- stamping

        public void Stamp(RagdollPawn pawn, SkillGrade g, bool sword)
        {
            if (pawn == null || g == SkillGrade.None) return;
            // Sword Fight shows no marks over the heads (10-25, 승규 님: "위에 아이콘이 뜨잖아 그거 다 지워줘").
            if (sword && !SwordFightSkillFx.HeadMarks) return;
            if (!Ready()) return;
            if (live.TryGetValue(pawn, out var old)) Drop(old);
            var s = new Live { pawn = pawn, g = g, sword = sword, born = clock };
            var tex = Glyph(SkillGrades.Symbol(g));
            s.aspect = tex.width / (float)tex.height;
            s.glyph = Quad("Skill grade " + SkillGrades.Symbol(g), front, out s.glyphR);
            s.gb = new MaterialPropertyBlock();
            s.gb.SetTexture("_MainTex", tex);
            s.gb.SetColor("_Fill", FillOf(g));
            s.gb.SetColor("_InkColor", Ink);
            s.ring = Quad("Skill grade ring", back, out s.ringR);
            s.rb = new MaterialPropertyBlock();
            s.rb.SetTexture("_MainTex", ringTex);
            s.rb.SetColor("_Fill", Ivory);
            s.rb.SetColor("_InkColor", Ink);
            if (g == SkillGrade.Brilliant || g == SkillGrade.Great)
            {
                s.rays = Quad("Skill grade rays", back, out s.raysR);
                s.yb = new MaterialPropertyBlock();
                s.yb.SetTexture("_MainTex", g == SkillGrade.Brilliant ? raysTex : ticksTex);
                s.yb.SetColor("_Fill", g == SkillGrade.Brilliant ? Ray : Ivory);
                s.yb.SetColor("_InkColor", Ink);
            }
            live[pawn] = s;
            if (sword && SwordFx != null) SwordFx.HoldMarkForGrade(pawn, LifeOf(g));
        }

        SwordFightSkillFx SwordFx
        {
            get
            {
                if (swordFx == null && !swordLooked) { swordLooked = true; swordFx = FindAnyObjectByType<SwordFightSkillFx>(); }
                return swordFx;
            }
        }

        void Drop(Live s)
        {
            if (s.glyph != null) Destroy(s.glyph.gameObject);
            if (s.ring != null) Destroy(s.ring.gameObject);
            if (s.rays != null) Destroy(s.rays.gameObject);
        }

        Transform Quad(string name, Material m, out Renderer r)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.enabled = false;
            r = mr;
            return go.transform;
        }

        // ---------------------------------------------------------------- the motion (the mock-up page's, frame for frame)

        /// <summary>The stamp at <paramref name="t"/> s: offset up in symbol heights, squash, roll (rad), alpha, white.</summary>
        static bool Pose(float t, SkillGrade g, out float dy, out float sx, out float sy, out float rot, out float a, out float flash)
        {
            dy = 0f; sx = sy = 1f; rot = 0f; a = 1f; flash = 0f;
            float hold = HoldOf(g);
            if (t < 0f || t > hold + 0.25f) return false;
            if (t < 0.09f) { float k = t / 0.09f, e = k * k; dy = 1.3f * (1f - e); sx = sy = 1.5f - 0.5f * e; a = Mathf.Clamp01(k * 2.5f); }
            else if (t < 0.13f) { sx = 1.28f; sy = 0.72f; flash = 1f; }   // frozen squash: the "팍"
            else
            {
                float k = Mathf.Clamp01((t - 0.13f) / 0.22f), w = (1f - k) * (1f - k) * Mathf.Cos(k * Mathf.PI * 3f);
                sx = 1f + 0.28f * w; sy = 1f - 0.28f * w; flash = Mathf.Clamp01(1f - (t - 0.13f) / 0.07f);
                float h = t - 0.35f;
                if (h > 0f)
                {
                    float u = Mathf.Clamp01(h / 0.25f);
                    if (g == SkillGrade.Brilliant) dy = 0.05f * Mathf.Sin(h * 6f);
                    if (g == SkillGrade.Mistake) rot = -0.16f * u * (2f - u);
                    if (g == SkillGrade.Interesting) rot = 0.24f * Mathf.Sin(Mathf.Clamp01(h / 0.45f) * Mathf.PI);
                    if (g == SkillGrade.Blunder) rot = 0.34f * Mathf.Sin(h * 17f) * Mathf.Exp(-h * 4f) - 0.2f * Mathf.Clamp01(h / 0.4f);
                }
            }
            if (t > hold) { float k = Mathf.Clamp01((t - hold) / 0.25f); dy += 0.5f * k; a *= 1f - k; sx *= 1f - 0.15f * k; sy *= 1f - 0.15f * k; }
            return true;
        }

        void LateUpdate()
        {
            clock += Time.captureFramerate > 0 ? 1f / Time.captureFramerate : Time.unscaledDeltaTime;
            if (live.Count == 0) return;
            var cam = ViewCamera();
            done.Clear();
            foreach (var kv in live)
            {
                var s = kv.Value;
                float t = clock - s.born;
                if (s.pawn == null || cam == null || !Pose(t, s.g, out float dy, out float sx, out float sy, out float rot, out float a, out float flash))
                {
                    done.Add(kv.Key);
                    continue;
                }
                Place(s, cam, t, dy, sx, sy, rot, a, flash);
            }
            foreach (var p in done) { if (live.TryGetValue(p, out var s)) Drop(s); live.Remove(p); }
        }

        void Place(Live s, Camera cam, float t, float dy, float sx, float sy, float rot, float a, float flash)
        {
            Transform view = cam.transform;
            Vector3 center;
            float h;   // the symbol's height in metres
            if (s.sword && SwordFx != null && SwordFx.MarkCenter(s.pawn, out Vector3 token, out float tokenSize))
            {
                // On the token's lower right, sized with it, but never under 36 px (the token is small from afar).
                float dist = Mathf.Max(0.5f, Vector3.Dot(token - view.position, view.forward));
                float least = cam.orthographic ? cam.orthographicSize * 2f * 36f / Screen.height
                    : dist * 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 36f / Screen.height;
                h = Mathf.Max(tokenSize * 0.62f, least) * (s.g == SkillGrade.Brilliant ? 1.15f : 1f);
                center = token + view.right * (tokenSize * 0.4f + h * 0.35f) - view.up * (tokenSize * 0.25f + h * 0.15f);
            }
            else
            {
                var head = s.pawn.bodies[(int)BodyId.Head];
                var col = head != null ? head.GetComponent<Collider>() : null;
                float top = col != null ? col.bounds.max.y : (head != null ? head.position.y + 0.12f : s.pawn.Hips.position.y + 0.8f);
                Vector3 above = new Vector3(head != null ? head.position.x : s.pawn.Hips.position.x, top, head != null ? head.position.z : s.pawn.Hips.position.z);
                // A steady size on screen: about 5.5 % of its height, never under 48 px.
                float dist = Mathf.Max(0.5f, Vector3.Dot(above - view.position, view.forward));
                float px = Mathf.Max(48f, Screen.height * 0.055f) * (s.g == SkillGrade.Brilliant ? 1.2f : 1f);
                h = cam.orthographic ? cam.orthographicSize * 2f * px / Screen.height
                    : dist * 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * px / Screen.height;
                center = above + Vector3.up * (0.3f + h * 0.7f);
            }
            if (s.g == SkillGrade.Brilliant && t > 0.1f && t < 0.35f)
            {
                float m = 0.06f * h * (1f - (t - 0.1f) / 0.25f);   // it shakes as it lands
                center += view.right * Random.Range(-m, m) + view.up * Random.Range(-m, m);
            }
            Quaternion face = view.rotation;
            // The mask covers 1.4 symbol heights (room for the ink and the shadow).
            s.glyph.SetPositionAndRotation(center + view.up * (dy * h), face * Quaternion.Euler(0f, 0f, rot * Mathf.Rad2Deg));
            s.glyph.localScale = new Vector3(1.4f * h * s.aspect * sx, 1.4f * h * sy, 1f);
            s.gb.SetFloat("_Flash", flash);
            s.gb.SetFloat("_Alpha", a);
            s.glyphR.SetPropertyBlock(s.gb);
            s.glyphR.enabled = a > 0.01f;

            // The ring (and the rays) behind it, from the landing on.
            bool dull = s.g == SkillGrade.Mistake || s.g == SkillGrade.Blunder;
            float rk = Mathf.Clamp01((t - 0.1f) / 0.45f), re = 1f - Mathf.Pow(1f - rk, 3f);
            bool ringOn = t > 0.1f && t < 0.55f && s.g != SkillGrade.Interesting;
            float reach = s.g == SkillGrade.Brilliant ? 2.6f : dull ? 1.1f : 1.8f;
            s.ring.SetPositionAndRotation(center, face);
            s.ring.localScale = Vector3.one * h * (0.9f + reach * re);
            s.rb.SetFloat("_Alpha", (1f - rk) * (dull ? 0.5f : 1f));
            s.ringR.SetPropertyBlock(s.rb);
            s.ringR.enabled = ringOn;
            if (s.rays != null)
            {
                float yk = Mathf.Clamp01((t - 0.1f) / 0.5f);
                s.rays.SetPositionAndRotation(center, face * Quaternion.Euler(0f, 0f, s.g == SkillGrade.Brilliant ? t * 85f : 15f));
                s.rays.localScale = Vector3.one * h * (1.6f + (s.g == SkillGrade.Brilliant ? 1.6f : 1f) * (1f - Mathf.Pow(1f - yk, 3f)));
                s.yb.SetFloat("_Alpha", 1f - yk);
                s.raysR.SetPropertyBlock(s.yb);
                s.raysR.enabled = t > 0.1f && t < 0.6f;
            }
        }

        static Camera ViewCamera()
        {
            if (PawnRushSkillFx.ViewOverride != null) return PawnRushSkillFx.ViewOverride;
            if (QueenHillSkillFx.ViewOverride != null) return QueenHillSkillFx.ViewOverride;
            if (SwordFightSkillFx.ViewOverride != null) return SwordFightSkillFx.ViewOverride;
            var rig = FindAnyObjectByType<LabCamera>();
            return rig != null && rig.Cam != null && rig.Cam.isActiveAndEnabled ? rig.Cam : Camera.main;
        }

        // ---------------------------------------------------------------- the masks, drawn once

        bool Ready()
        {
            if (front != null) return true;
            var shader = Shader.Find("ChessFight/Skill Grade");
            if (shader == null) shader = Resources.Load<Shader>("SkillGradeFx/SkillGrade");
            if (shader == null) { Debug.LogWarning("[R128] ChessFight/Skill Grade shader missing"); return false; }
            back = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, renderQueue = 3059 };
            front = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, renderQueue = 3061 };
            quad = new Mesh { name = "Skill grade quad", hideFlags = HideFlags.HideAndDontSave };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(0.5f, 0.5f), new Vector3(-0.5f, 0.5f) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            quad.RecalculateBounds();
            ringTex = Bake(128, 128, p => Mathf.Abs(p.magnitude - 0.36f) - 0.05f, 0.035f, Vector2.zero);
            raysTex = Bake(128, 128, p => Rays(p, 12, 0.30f, 0.44f, 0.022f), 0.02f, Vector2.zero);
            ticksTex = Bake(128, 128, p => Rays(p, 6, 0.32f, 0.44f, 0.026f), 0.022f, Vector2.zero);
            return true;
        }

        void OnDestroy()
        {
            foreach (var s in live.Values) Drop(s);
            foreach (var t in glyphs.Values) if (t != null) Destroy(t);
            foreach (var o in new Object[] { ringTex, raysTex, ticksTex, front, back, quad }) if (o != null) Destroy(o);
        }

        /// <summary>A mask from a signed distance (negative inside) over the square ±0.5 (units of the texture's height,
        /// x widened by the aspect): r = inside, g = inside the ink edge, b = the ink edge's shadow at <paramref name="shadow"/>.</summary>
        static Texture2D Bake(int w, int h, System.Func<Vector2, float> sdf, float ink, Vector2 shadow)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[w * h];
            float aa = 1f / h, half = w / (float)h * 0.5f;
            bool shade = shadow != Vector2.zero;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2((x + 0.5f) / h - half, (y + 0.5f) / h - 0.5f);
                float d = sdf(p);
                float r = Mathf.Clamp01(0.5f - d / aa), g = Mathf.Clamp01(0.5f - (d - ink) / aa);
                float b = shade ? Mathf.Clamp01(0.5f - (sdf(p - shadow) - ink) / aa) : 0f;
                px[y * w + x] = new Color32((byte)(r * 255f), (byte)(g * 255f), (byte)(b * 255f), 255);
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        static float Rays(Vector2 p, int n, float from, float to, float width)
        {
            float best = 1e9f;
            for (int i = 0; i < n; i++)
            {
                float an = (i + 0.5f) / n * Mathf.PI * 2f;
                var d = new Vector2(Mathf.Cos(an), Mathf.Sin(an));
                best = Mathf.Min(best, Segment(p, d * from, d * to) - width);
            }
            return best;
        }

        // The symbols, drawn as chunky slanted toy shapes: "!" a bar and a dot, "?" a hook, a short neck and a dot.
        // In symbol heights: the baseline at 0, the top at 1; the mask covers -0.2 .. 1.2.
        const float Slant = 0.12f, Bang = 0.36f, Query = 0.7f;

        Texture2D Glyph(string symbol)
        {
            if (glyphs.TryGetValue(symbol, out var tex) && tex != null) return tex;
            float width = 0f;
            foreach (char ch in symbol) width += ch == '?' ? Query : Bang;
            float span = width + 0.6f;   // room for the ink, the shadow and the slant
            const int H = 160;
            int W = Mathf.CeilToInt(span / 1.4f * H);
            tex = Bake(W, H, p => SymbolDistance(symbol, width, new Vector2(p.x * 1.4f, p.y * 1.4f + 0.5f)) / 1.4f, 0.07f / 1.4f, new Vector2(0.06f, -0.07f) / 1.4f);
            tex.name = "Skill grade " + symbol;
            glyphs[symbol] = tex;
            return tex;
        }

        static float SymbolDistance(string symbol, float width, Vector2 p)
        {
            p.x -= Slant * (p.y - 0.5f);   // undo the slant
            float x = -width * 0.5f, best = 1e9f;
            foreach (char ch in symbol)
            {
                float adv = ch == '?' ? Query : Bang;
                var q = new Vector2(p.x - (x + adv * 0.5f), p.y);
                best = Mathf.Min(best, ch == '?' ? QueryDistance(q) : BangDistance(q));
                x += adv;
            }
            return best;
        }

        static float BangDistance(Vector2 p) =>
            Mathf.Min(Segment(p, new Vector2(0f, 0.5f), new Vector2(0f, 0.9f)) - 0.12f, (p - new Vector2(0f, 0.1f)).magnitude - 0.11f);

        static float QueryDistance(Vector2 p)
        {
            const float R = 0.2f, W = 0.105f, A0 = -60f * Mathf.Deg2Rad, A1 = 165f * Mathf.Deg2Rad;
            var c = new Vector2(0f, 0.69f);
            var r = p - c;
            float an = Mathf.Atan2(r.y, r.x), arc;
            if (an >= A0 && an <= A1) arc = Mathf.Abs(r.magnitude - R) - W;
            else
            {
                var e0 = c + new Vector2(Mathf.Cos(A0), Mathf.Sin(A0)) * R;
                var e1 = c + new Vector2(Mathf.Cos(A1), Mathf.Sin(A1)) * R;
                arc = Mathf.Min((p - e0).magnitude, (p - e1).magnitude) - W;
            }
            var end = c + new Vector2(Mathf.Cos(A0), Mathf.Sin(A0)) * R;
            float neck = Segment(p, end, new Vector2(0f, 0.47f)) - W;
            float dot = (p - new Vector2(0f, 0.1f)).magnitude - 0.11f;
            return Mathf.Min(arc, Mathf.Min(neck, dot));
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return (p - (a + ab * k)).magnitude;
        }
    }
}
