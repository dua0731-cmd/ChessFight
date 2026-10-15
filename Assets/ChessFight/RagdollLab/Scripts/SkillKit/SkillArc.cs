using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Electric arcs (R142, 승규 님 10-15, of the Pawn Rush queen's eight-way push: "지금 공기 충격파 좋은데 전기+공기 충격파 같은
    /// 거였으면 좋겠어"): jagged bolts of lightning, each a white-hot thread in a body of its colour and a soft glow beyond
    /// (<c>ChessFight/Skill Arc</c>), forking once or twice, that strike again every other frame between the same ends (a new
    /// jagged path, a flicker in brightness, now and then a frame dark) and are gone in a few hundredths of a second. Made by
    /// midpoint displacement and drawn as ribbons turned to the camera, all in one mesh. Nothing round (승규 님: no rings,
    /// discs or bubbles at hits). Its owner steps it in its own effect time and calls <see cref="Strike"/>.
    /// R143 (승규 님 10-15, a screen recording of the queen in play: "적용 된거야 난 안된거 같은데"): in play they were faint pale
    /// threads, there for a tenth of a second. The flicker and the going out faded the whole bolt, and a half-faded gold over
    /// the white floor is a pale cream. Now a bolt stays solid while it lives and thins out as it ends; its flicker only heats
    /// its core; a faint one is thinner rather than paler; however far the camera, it is never thinner than a share of the
    /// view; and the arcs light what is round them, a gold point light where they are that flickers with them and goes out
    /// with the last of them.
    /// </summary>
    public class SkillArc
    {
        class Bolt
        {
            public Vector3 a, b;
            public Color tint;
            public float age, life, width, jag, bright, shown, next;
            public int forks;
            public readonly List<Vector3> path = new List<Vector3>();
            public readonly List<Vector3>[] forkPaths = { new List<Vector3>(), new List<Vector3>() };
            public readonly float[] forkWidths = new float[2];
        }

        /// <summary>A new jagged path every other frame (at 60 a second).</summary>
        const float Restrike = 2f / 60f;

        /// <summary>R143: the narrowest a bolt is drawn, a share of the view's height where it is (from far off they were
        /// hairlines).</summary>
        const float MinView = 0.006f;

        /// <summary>R143: the light the arcs give: intensity per metre of bolt width times its length (up to 1.5 m) at full
        /// brightness, its most, how far it reaches and how far above the arcs it hangs.</summary>
        const float LightGain = 1.1f, LightMost = 2.6f, LightRange = 4.5f, LightLift = 0.35f;

        readonly Material mat;
        readonly Mesh mesh;
        readonly Transform t;
        readonly MeshRenderer r;
        readonly Light glow;
        readonly List<Bolt> live = new List<Bolt>();
        readonly List<Bolt> spare = new List<Bolt>();
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> tris = new List<int>();
        static readonly List<Vector3> scratch = new List<Vector3>();

        public SkillArc(Transform parent)
        {
            var shader = Shader.Find("ChessFight/Skill Arc");
            if (shader == null) shader = Resources.Load<Shader>("PawnRushSkillFx/SkillArc");
            if (shader == null) return;
            mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            mesh = new Mesh { name = "Skill arcs" };
            mesh.MarkDynamic();
            var go = new GameObject("Skill arcs");
            t = go.transform;
            if (parent != null) t.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;
            var lit = new GameObject("Skill arcs light");
            lit.transform.SetParent(t, false);
            glow = lit.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.range = LightRange;
            glow.shadows = LightShadows.None;
            glow.renderMode = LightRenderMode.ForcePixel;
            glow.enabled = false;
        }

        /// <summary>The shader was found (otherwise nothing is drawn).</summary>
        public bool Ready => mat != null;

        public int Count => live.Count;

        /// <summary>
        /// A bolt from <paramref name="a"/> to <paramref name="b"/> for <paramref name="life"/> seconds, <paramref name="width"/>
        /// across its glow (its body about two thirds of that, its white core a fifth), thinning to its tip; <paramref name="bright"/>
        /// how hot it burns (R143: a faint one is thinner and its core goes gold, it fades only below 0.4); <paramref name="jag"/>
        /// how far it strays (a share of each stretch it crosses), <paramref name="forks"/> branches (0..2) off its first two thirds.
        /// </summary>
        public void Strike(Vector3 a, Vector3 b, Color tint, float width = 0.08f, float life = 0.07f, float bright = 1f, float jag = 0.22f,
                           int forks = 1)
        {
            if (!Ready || (b - a).sqrMagnitude < 1e-6f) return;
            Bolt bolt;
            if (spare.Count > 0)
            {
                bolt = spare[spare.Count - 1];
                spare.RemoveAt(spare.Count - 1);
            }
            else bolt = new Bolt();
            bolt.a = a;
            bolt.b = b;
            bolt.tint = tint;
            bolt.width = width;
            bolt.life = Mathf.Max(0.02f, life);
            bolt.bright = Mathf.Clamp01(bright);
            bolt.jag = jag;
            bolt.forks = Mathf.Clamp(forks, 0, 2);
            bolt.age = 0f;
            bolt.next = Restrike;
            Shape(bolt, true);
            live.Add(bolt);
        }

        /// <summary>One frame of every bolt (<paramref name="dt"/> in the owner's effect time; 0 holds them), turned to
        /// <paramref name="cam"/>.</summary>
        public void Step(float dt, Camera cam)
        {
            if (!Ready) return;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var bolt = live[i];
                bolt.age += dt;
                if (bolt.age >= bolt.life)
                {
                    live.RemoveAt(i);
                    spare.Add(bolt);
                    continue;
                }
                bolt.next -= dt;
                if (bolt.next <= 0f)
                {
                    bolt.next += Restrike;
                    Shape(bolt, false);
                }
            }
            if (live.Count == 0 || cam == null)
            {
                r.enabled = false;
                glow.enabled = false;
                return;
            }
            verts.Clear();
            colors.Clear();
            uvs.Clear();
            tris.Clear();
            Vector3 eye = cam.transform.position;
            // The view's height one metre out (the narrowest a bolt is drawn goes with its distance), or all of it.
            float view = cam.orthographic ? 0f : 2f * Mathf.Tan(0.5f * cam.fieldOfView * Mathf.Deg2Rad);
            float least = cam.orthographic ? MinView * 2f * cam.orthographicSize : 0f;
            float power = 0f;
            Vector3 lightAt = Vector3.zero;
            Color lightTint = Color.black;
            foreach (var bolt in live)
            {
                if (bolt.shown <= 0f) continue;
                // Solid while it lives, thinning out as it ends; a faint one thinner, its core gold rather than white.
                float k = bolt.age / bolt.life;
                float thin = (1f - k * k * k) * Mathf.Lerp(0.55f, 1f, bolt.bright);
                float heat = bolt.shown * bolt.bright;
                float cover = Mathf.Clamp01(bolt.bright * 2.5f);
                Ribbon(bolt.path, bolt.width, thin, bolt.tint, heat, cover, eye, view, least);
                for (int f = 0; f < bolt.forks; f++) Ribbon(bolt.forkPaths[f], bolt.forkWidths[f], thin, bolt.tint, heat * 0.85f, cover, eye, view, least);
                float w = bolt.width * Mathf.Min(Vector3.Distance(bolt.a, bolt.b), 1.5f) * heat * thin;
                power += w;
                lightAt += (bolt.a + bolt.b) * (0.5f * w);
                lightTint += bolt.tint * w;
            }
            var toLocal = t.worldToLocalMatrix;
            for (int i = 0; i < verts.Count; i++) verts[i] = toLocal.MultiplyPoint3x4(verts[i]);
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            r.enabled = verts.Count > 0;
            // The light they give, where they are and as bright as they burn this frame (so it flickers with them).
            glow.enabled = power > 1e-4f;
            if (glow.enabled)
            {
                glow.transform.position = lightAt / power + Vector3.up * LightLift;
                Color tint = lightTint / power;
                glow.color = Color.Lerp(new Color(tint.r, tint.g, tint.b, 1f), Color.white, 0.25f);
                glow.intensity = Mathf.Min(LightMost, power * LightGain);
            }
        }

        /// <summary>A new jagged path between the bolt's ends, its forks and how bright it shows until the next one (now
        /// and then a frame dark, never the first).</summary>
        void Shape(Bolt bolt, bool first)
        {
            float len = Vector3.Distance(bolt.a, bolt.b);
            int levels = len < 0.2f ? 3 : len < 0.6f ? 4 : 5;
            Jagged(bolt.path, bolt.a, bolt.b, bolt.jag, levels);
            int n = bolt.path.Count;
            for (int f = 0; f < bolt.forks; f++)
            {
                int i = Mathf.Clamp(Random.Range(Mathf.RoundToInt(n * 0.2f), Mathf.RoundToInt(n * 0.65f)), 0, n - 2);
                Vector3 from = bolt.path[i];
                Vector3 on = bolt.path[Mathf.Min(i + 2, n - 1)] - from;
                if (on.sqrMagnitude < 1e-8f) on = bolt.b - bolt.a;
                on.Normalize();
                Vector3 axis = Vector3.Cross(on, Random.onUnitSphere);
                if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(on, Vector3.up);
                Vector3 way = Quaternion.AngleAxis(Random.Range(25f, 50f), axis.normalized) * on;
                Jagged(bolt.forkPaths[f], from, from + way * len * Random.Range(0.22f, 0.42f), bolt.jag * 1.15f, Mathf.Max(2, levels - 1));
                bolt.forkWidths[f] = bolt.width * Random.Range(0.45f, 0.6f);
            }
            // R143: the flicker only (how hot this stroke burns); how bright the bolt is overall is its own.
            bolt.shown = !first && Random.value < 0.12f ? 0f : Random.Range(0.6f, 1f);
        }

        /// <summary>A path from <paramref name="a"/> to <paramref name="b"/> broken <paramref name="levels"/> times over: each
        /// stretch's middle pushed off it sideways by up to <paramref name="jag"/> of its length.</summary>
        static void Jagged(List<Vector3> path, Vector3 a, Vector3 b, float jag, int levels)
        {
            path.Clear();
            path.Add(a);
            path.Add(b);
            for (int level = 0; level < levels; level++)
            {
                scratch.Clear();
                for (int i = 0; i < path.Count - 1; i++)
                {
                    Vector3 p = path[i], q = path[i + 1], d = q - p;
                    float len = d.magnitude;
                    Vector3 off = len > 1e-5f ? Vector3.Cross(d / len, Random.onUnitSphere) : Vector3.zero;
                    scratch.Add(p);
                    scratch.Add((p + q) * 0.5f + off * (Random.Range(-1f, 1f) * jag * len));
                }
                scratch.Add(path[path.Count - 1]);
                path.Clear();
                path.AddRange(scratch);
            }
        }

        /// <summary>A path as a ribbon turned to the eye: <paramref name="width"/> (never under <see cref="MinView"/> of the
        /// view where it is: <paramref name="view"/> is the view's height a metre out, <paramref name="least"/> a fixed least
        /// for an orthographic one) times <paramref name="thin"/>, thick where it leaves its source, thinning to its tip, its
        /// very ends pinched; uv x how hot it burns (<paramref name="heat"/>), uv y across it (the shader's core and glow),
        /// the colour's alpha how much of what is behind it hides (<paramref name="cover"/>).</summary>
        void Ribbon(List<Vector3> path, float width, float thin, Color tint, float heat, float cover, Vector3 eye, float view, float least)
        {
            int n = path.Count;
            if (n < 2) return;
            int first = verts.Count;
            var c = new Color(tint.r, tint.g, tint.b, cover);
            for (int i = 0; i < n; i++)
            {
                Vector3 p = path[i];
                Vector3 along = path[Mathf.Min(i + 1, n - 1)] - path[Mathf.Max(i - 1, 0)];
                Vector3 side = Vector3.Cross(along, eye - p);
                if (side.sqrMagnitude < 1e-12f) side = Vector3.Cross(along, Vector3.up);
                side = side.sqrMagnitude > 1e-12f ? side.normalized : Vector3.right;
                float u = i / (float)(n - 1);
                float wide = Mathf.Max(width, least, MinView * view * Vector3.Distance(eye, p));
                float w = 0.5f * wide * thin * Mathf.Lerp(1f, 0.4f, u) * Mathf.Clamp01(Mathf.Min(u, 1f - u) * 10f + 0.35f);
                verts.Add(p - side * w);
                verts.Add(p + side * w);
                colors.Add(c);
                colors.Add(c);
                uvs.Add(new Vector2(heat, 0f));
                uvs.Add(new Vector2(heat, 1f));
            }
            for (int i = 0; i < n - 1; i++)
            {
                int a = first + i * 2;
                tris.Add(a);
                tris.Add(a + 2);
                tris.Add(a + 1);
                tris.Add(a + 1);
                tris.Add(a + 2);
                tris.Add(a + 3);
            }
        }

        public void Destroy()
        {
            live.Clear();
            spare.Clear();
            if (t != null) Object.Destroy(t.gameObject);
            if (mesh != null) Object.Destroy(mesh);
            if (mat != null) Object.Destroy(mat);
        }
    }
}
