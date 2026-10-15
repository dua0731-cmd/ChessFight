using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Real dust for the skill effects (R138, 승규 님 10-14: "임펙트를 좀 더 사실 적으로 표현해줘 저 사진은 너무 운석이나 돌맹이
    /// 같아 보여"): the inked cartoon puffs a knockdown, a landing or a crash threw up read as meteors and pebbles, so they
    /// are drawn as dust instead: soft sunlit billows (<c>ChessFight/Skill Dust</c>) that are thrown out fast, slow down
    /// at once, keep growing and drift up a little, fray from their edges and fade, with a few flecks of grit that fall.
    /// One mesh of camera-facing quads, sorted back to front and made again each frame. The same calls as the kits' puffs
    /// (<see cref="Burst"/> for PuffBurst, <see cref="Ring"/> for DustRing), so a mode switches over by handing its puffs
    /// here: the Pawn Rush effects through <see cref="SkillInkKit.UseRealDust"/>, the Queen of the Hill effects directly.
    /// </summary>
    public class SkillDust
    {
        struct Mote
        {
            public Vector3 pos, vel;
            public float size, grow, age, life, drag, rise, density, seed, turn, spin, floorY;
            public Color lit, shade;
            public bool grit;
        }

        /// <summary>At most this many at once (the oldest go first).</summary>
        const int Max = 900;
        /// <summary>How fast a mote slows (1/s): thrown out hard, it has gone most of its way in a quarter of a second.</summary>
        const float Drag = 4.5f;
        /// <summary>How much dust a burst or a ring throws, against R138's first take (R139, 승규 님 10-14: "먼지 양을 좀
        /// 줄여줘"): a little over half the billows, a little thinner and a little sooner gone, half the grit.</summary>
        public const float Amount = 0.55f;

        readonly List<Mote> motes = new List<Mote>();
        readonly List<int> order = new List<int>();
        readonly List<float> depth = new List<float>();
        readonly GameObject go;
        readonly Mesh mesh;
        readonly Material mat;
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<Vector2> uv0 = new List<Vector2>();
        readonly List<Vector4> uv1 = new List<Vector4>();
        readonly List<Vector4> uv2 = new List<Vector4>();
        readonly List<int> tris = new List<int>();

        /// <summary>The dust's own warm greys; a palette only tints them.</summary>
        static readonly Color DustLit = new Color(0.93f, 0.9f, 0.85f), DustShade = new Color(0.47f, 0.43f, 0.38f);

        public SkillDust(Transform parent)
        {
            var shader = Shader.Find("ChessFight/Skill Dust");
            if (shader == null) shader = Resources.Load<Shader>("PawnRushSkillFx/SkillDust");
            if (shader == null) return;
            mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            go = new GameObject("Skill dust");
            if (parent != null) go.transform.SetParent(parent, false);
            mesh = new Mesh { name = "Skill dust" };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>The shader was found (otherwise the callers keep their own puffs).</summary>
        public bool Ready => mat != null;

        public int Count => motes.Count;

        /// <summary>Dust's lit and shaded colours, tinted a little toward a palette's light and dark colours.</summary>
        public static void Tint(Color light, Color deep, out Color lit, out Color shade, float amount = 0.35f)
        {
            lit = Color.Lerp(DustLit, light, amount);
            shade = Color.Lerp(DustShade, deep, amount);
            lit.a = shade.a = 1f;
        }

        /// <summary>One billow: <paramref name="size"/> its radius (m) at first, <paramref name="grow"/> how much more it
        /// becomes, <paramref name="rise"/> m/s up (or down), <paramref name="density"/> how thick it is at its middle.</summary>
        public void Emit(Vector3 pos, Vector3 vel, float size, float life, Color lit, Color shade, float grow = 1.4f,
                         float rise = 0.2f, float density = 0.85f, float floorY = float.NegativeInfinity, float drag = Drag)
        {
            if (!Ready) return;
            if (motes.Count >= Max) motes.RemoveAt(0);
            motes.Add(new Mote
            {
                pos = pos, vel = vel, size = Mathf.Max(0.01f, size), grow = grow, life = Mathf.Max(0.05f, life), drag = drag,
                rise = rise, density = density, seed = Random.Range(0f, 60f), turn = Random.Range(0f, 6.283f),
                spin = Random.Range(-0.9f, 0.9f), floorY = floorY, lit = lit, shade = shade,
            });
        }

        /// <summary>A fleck of grit thrown out of the dust: falls, stops on the floor, fades.</summary>
        public void Grit(Vector3 pos, Vector3 vel, float size, float life, Color color, float floorY = float.NegativeInfinity)
        {
            if (!Ready) return;
            if (motes.Count >= Max) motes.RemoveAt(0);
            motes.Add(new Mote { pos = pos, vel = vel, size = size, life = life, density = 1f, floorY = floorY, lit = color, shade = color, grit = true });
        }

        /// <summary>The floor under a point (a pawn or a loose body is not a floor), or -infinity.</summary>
        public static float FloorY(Vector3 at, float above = 0.6f)
        {
            return SkillInkKit.FloorUnder(at, above, out Vector3 floor) ? floor.y : float.NegativeInfinity;
        }

        /// <summary>
        /// The puffs of a burst, as dust (the kits' PuffBurst: <paramref name="n"/> puffs of <paramref name="size"/> across
        /// thrown <paramref name="radius"/> out from <paramref name="at"/>, rising <paramref name="up"/> over
        /// <paramref name="life"/>): about one and a half billows a puff since R139 (three in R138), twice its size and
        /// lasting longer (dust hangs), and a fleck of grit or two.
        /// </summary>
        public void Burst(Vector3 at, Color light, Color deep, int n, float radius, float size, float life = 0.45f, float up = 0.3f)
        {
            if (!Ready || n <= 0) return;
            Tint(light, deep, out var lit, out var shade);
            float floor = FloorY(at);
            int count = Mathf.Clamp(Mathf.RoundToInt(n * 3 * Amount), 2, 16);
            float last = Mathf.Max(0.5f, life * 1.5f);
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.6f;
                dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.up;
                float far = radius * Random.Range(0.55f, 1.15f);
                Emit(at + dir * (size * 0.3f), dir * (far * Drag), size * Random.Range(0.7f, 1.05f), last * Random.Range(0.85f, 1.15f),
                     lit, shade, Random.Range(1f, 1.45f), up / Mathf.Max(0.1f, last) + 0.12f, Random.Range(0.62f, 0.8f), floor);
            }
            for (int i = 0; i < Mathf.Clamp(n / 3 + 1, 1, 4); i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y);
                Grit(at, dir * Random.Range(1.2f, 2.6f) + Vector3.up * Random.Range(1f, 2.2f), Random.Range(0.012f, 0.022f), Random.Range(0.5f, 0.8f), shade, floor);
            }
        }

        /// <summary>
        /// A ring of dust rolling out over the floor (the kits' DustRing: <paramref name="n"/> puffs out to
        /// <paramref name="radius"/>): an impact's dust thrown out all round, hugging the floor and slowing, growing and
        /// lifting a little, with a few slower billows rising where it hit and grit flung out.
        /// </summary>
        public void Ring(Vector3 at, int n, float radius, Color light, Color deep, float size = 0.3f, float life = 0.5f)
        {
            if (!Ready || n <= 0) return;
            Tint(light, deep, out var lit, out var shade);
            float floor = FloorY(at + Vector3.up * 0.2f);
            if (float.IsNegativeInfinity(floor)) floor = at.y;
            int count = Mathf.Clamp(Mathf.RoundToInt(n * 3 * Amount), 6, 22);
            float start = Random.Range(0f, 360f), last = Mathf.Max(0.75f, life * 1.75f);
            for (int i = 0; i < count; i++)
            {
                float ang = (start + (i + Random.Range(-0.35f, 0.35f)) * 360f / count) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                var swirl = new Vector3(-dir.z, 0f, dir.x) * Random.Range(-0.4f, 0.4f);
                // (The first take's ring was a little heavy: it hid a fallen piece for a second.)
                float s = size * Random.Range(0.55f, 0.9f), far = radius * Random.Range(0.8f, 1.1f);
                Emit(new Vector3(at.x, floor + s * 0.45f, at.z) + dir * (s * 0.4f), (dir * far + swirl) * Drag + Vector3.up * Random.Range(0.25f, 0.6f),
                     s, last * Random.Range(0.8f, 1.05f), lit, shade, Random.Range(1.15f, 1.5f), Random.Range(0.08f, 0.22f), Random.Range(0.52f, 0.7f), floor);
            }
            for (int i = 0; i < 2; i++)
                Emit(new Vector3(at.x, floor + size * 0.6f, at.z) + Random.insideUnitSphere * (size * 0.4f),
                     Random.insideUnitSphere * 0.4f + Vector3.up * 0.45f, size, last, lit, shade, 1.1f, 0.25f, 0.62f, floor);
            for (int i = 0; i < Mathf.Clamp(n / 2, 1, 5); i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                Grit(new Vector3(at.x, floor + 0.05f, at.z), dir * Random.Range(1.5f, 3f) + Vector3.up * Random.Range(1.2f, 2.4f),
                     Random.Range(0.012f, 0.024f), Random.Range(0.6f, 0.9f), shade, floor);
            }
        }

        /// <summary>One frame: every mote moves, grows and fades; then the mesh, back to front for this camera.</summary>
        public void Step(float dt, Camera cam)
        {
            if (!Ready) return;
            float g = Physics.gravity.y;
            for (int i = motes.Count - 1; i >= 0; i--)
            {
                var m = motes[i];
                m.age += dt;
                if (m.age >= m.life) { motes.RemoveAt(i); continue; }
                if (m.grit)
                {
                    m.vel.y += g * dt;
                    m.pos += m.vel * dt;
                    if (m.pos.y < m.floorY + m.size)
                    {
                        m.pos.y = m.floorY + m.size;
                        m.vel = Vector3.zero;
                    }
                }
                else
                {
                    m.vel *= Mathf.Exp(-m.drag * dt);
                    m.pos += (m.vel + Vector3.up * m.rise) * dt;
                    m.turn += m.spin * dt;
                    float r = Radius(m);
                    if (m.pos.y < m.floorY + r * 0.35f)
                    {
                        m.pos.y = m.floorY + r * 0.35f;
                        if (m.vel.y < 0f) m.vel.y = 0f;
                    }
                }
                motes[i] = m;
            }
            Build(cam);
        }

        static float Radius(in Mote m) => m.size * (1f + m.grow * (1f - Mathf.Exp(-3.2f * m.age / m.life)));

        void Build(Camera cam)
        {
            verts.Clear();
            colors.Clear();
            uv0.Clear();
            uv1.Clear();
            uv2.Clear();
            tris.Clear();
            if (cam == null || motes.Count == 0)
            {
                mesh.Clear();
                return;
            }
            var view = cam.transform;
            Vector3 eye = view.position, right = view.right, up = view.up;
            order.Clear();
            depth.Clear();
            for (int i = 0; i < motes.Count; i++)
            {
                order.Add(i);
                depth.Add((motes[i].pos - eye).sqrMagnitude);
            }
            order.Sort((a, b) => depth[b].CompareTo(depth[a]));   // far ones first
            Vector3 lo = Vector3.one * float.MaxValue, hi = Vector3.one * float.MinValue;
            foreach (int i in order)
            {
                var m = motes[i];
                float k = m.age / m.life;
                float r = m.grit ? m.size : Radius(m);
                float shown = m.grit ? 1f - Mathf.Clamp01((k - 0.6f) / 0.4f)
                                     : Mathf.Clamp01(m.age / 0.06f) * (1f - Smooth(Mathf.Clamp01((k - 0.4f) / 0.6f)));
                Vector3 rx = right * r, ry = up * r;
                int b = verts.Count;
                verts.Add(m.pos - rx - ry);
                verts.Add(m.pos + rx - ry);
                verts.Add(m.pos + rx + ry);
                verts.Add(m.pos - rx + ry);
                var c = new Color(m.lit.r, m.lit.g, m.lit.b, shown);
                var d = new Vector4(m.seed, k, m.turn, m.density);
                var s = new Vector4(m.shade.r, m.shade.g, m.shade.b, m.grit ? 1f : 0f);
                for (int j = 0; j < 4; j++)
                {
                    colors.Add(c);
                    uv1.Add(d);
                    uv2.Add(s);
                }
                uv0.Add(new Vector2(-1f, -1f));
                uv0.Add(new Vector2(1f, -1f));
                uv0.Add(new Vector2(1f, 1f));
                uv0.Add(new Vector2(-1f, 1f));
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
                lo = Vector3.Min(lo, m.pos - Vector3.one * r);
                hi = Vector3.Max(hi, m.pos + Vector3.one * r);
            }
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetUVs(2, uv2);
            mesh.SetTriangles(tris, 0);
            mesh.bounds = new Bounds((lo + hi) * 0.5f, hi - lo);
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        public void Clear() => motes.Clear();

        public void Destroy()
        {
            motes.Clear();
            if (go != null) Object.Destroy(go);
            if (mesh != null) Object.Destroy(mesh);
            if (mat != null) Object.Destroy(mat);
        }
    }
}
