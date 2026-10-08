using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The cartoon parts of the skill effects (R86, after 승규 님's three reference shorts: a stylized explosion, an
    /// electric dash and a hand-drawn wind burst). Puffs: lumpy spheres in hard colour bands that burn, glow at the
    /// edges, are inked and are eaten away (<c>ChessFight/Skill Toon</c>): the queen's fire and smoke, the knight's
    /// wind. Cracks: the queen's lines in the floor that run out hot and cool to scorch marks (<c>Skill Crack</c>,
    /// drawn here). Swooshes: ribbons of wind bent into arcs and swirls (<c>Skill Swoosh</c>). Lines: the rook's speed
    /// lines and the bright spindle ahead of it, and lightning that crawls along its path. Made in code, no art files.
    /// </summary>
    public partial class PawnRushSkillFx
    {
        Shader toonShader, crackShader, swooshShader;
        /// <summary>The queen's fire turning to dark smoke, and the dust her blast rolls over the floor; the knight's
        /// wind.</summary>
        Material matFire, matDust, matWind;
        /// <summary>A round sphere for the puffs (an icosphere: the lumps stay smooth, the UV sphere showed facets).</summary>
        Mesh meshPuff;
        Material matCrack, matScorch, matSwoosh, matFlare;
        /// <summary>The rook's cartoon lightning and speed lines: a white-hot middle, orange, an ink edge (Skill Swoosh
        /// with its bands from the middle). Light alone was lost on the white floor.</summary>
        Material matVolt;
        Texture2D texCracks, texScorch;
        Vector3 sunDir = new Vector3(0.35f, 0.79f, -0.5f);

        // The queen's smoke: charcoal, a little warm, so it reads on the white test floor and the cream squares alike
        // (a first try in warm browns looked like a lump of earth once the fire was out).
        static readonly Color SmokeLit = new Color(0.47f, 0.43f, 0.42f), SmokeMid = new Color(0.25f, 0.22f, 0.22f), SmokeShade = new Color(0.11f, 0.095f, 0.1f);
        // The knight's wind: near white, sky and a deep blue, inked in navy (bluer than a first try, which was lost on the
        // white floor as white cotton).
        static readonly Color WindLit = new Color(0.86f, 0.95f, 1f), WindMid = new Color(0.42f, 0.7f, 1f), WindShade = new Color(0.12f, 0.36f, 0.95f);
        static readonly Color WindInk = new Color(0.04f, 0.08f, 0.3f);

        void BuildToon()
        {
            toonShader = Find("ChessFight/Skill Toon", "PawnRushSkillFx/SkillToon");
            crackShader = Find("ChessFight/Skill Crack", "PawnRushSkillFx/SkillCrack");
            swooshShader = Find("ChessFight/Skill Swoosh", "PawnRushSkillFx/SkillSwoosh");

            matFire = Mat(toonShader);
            matFire.SetColor("_Lit", SmokeLit);
            matFire.SetColor("_Mid", SmokeMid);
            matFire.SetColor("_Shade", SmokeShade);
            matFire.SetVector("_Bands", new Vector4(0.44f, 0.76f, 0.06f, 0f));
            // Colours go in as sRGB and the project is linear, so above 1 they grow fast: the fire's body stays
            // under the bloom threshold, only its core blooms (the first try washed the whole picture white).
            matFire.SetColor("_Fire", new Color(1.25f, 0.62f, 0.2f));
            matFire.SetColor("_FireCore", new Color(1.6f, 1.3f, 0.82f));
            matFire.SetFloat("_RimPower", 2.4f);
            matFire.SetColor("_Bite", new Color(1.2f, 0.55f, 0.15f));
            matFire.SetFloat("_Scale", 1.8f);

            matDust = Mat(toonShader);
            matDust.CopyPropertiesFromMaterial(matFire);
            matDust.SetColor("_Lit", new Color(0.86f, 0.8f, 0.73f));
            matDust.SetColor("_Mid", new Color(0.64f, 0.58f, 0.53f));
            matDust.SetColor("_Shade", new Color(0.43f, 0.38f, 0.36f));
            matDust.SetColor("_Bite", new Color(0.43f, 0.38f, 0.36f));

            matWind = Mat(toonShader);
            matWind.SetColor("_Lit", WindLit);
            matWind.SetColor("_Mid", WindMid);
            matWind.SetColor("_Shade", WindShade);
            matWind.SetVector("_Bands", new Vector4(0.5f, 0.8f, 0.1f, 0f));
            matWind.SetFloat("_Ink", 0.32f);
            matWind.SetColor("_InkColor", WindInk);
            matWind.SetColor("_Bite", WindMid);   // fades out light: an inked bitten edge left scribbles behind
            matWind.SetFloat("_Scale", 1.9f);

            meshPuff = Icosphere(3);
            texCracks = DrawCracks(512, 7);
            texScorch = Tex(128, 128, TextureWrapMode.Clamp, (u, v) =>
            {
                float r = Radius(u, v) + (Fbm(u, v, 5, 4) - 0.5f) * 0.4f;
                return White(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((0.92f - r) / 0.4f)) * 0.9f);
            });
            matCrack = Mat(crackShader);
            matCrack.mainTexture = texCracks;
            matScorch = Glow(texScorch, opacity: 1f, noise: 0.3f, noiseST: new Vector4(2f, 2f, 0f, 0f));
            matSwoosh = Mat(swooshShader);
            matSwoosh.SetColor("_Lit", WindLit);
            matSwoosh.SetColor("_Mid", WindMid);
            matSwoosh.SetColor("_Shade", WindShade);
            matSwoosh.SetColor("_InkColor", WindInk);
            matFlare = Glow(texDot);
            matVolt = Mat(swooshShader);
            matVolt.SetFloat("_Symmetric", 1f);
            matVolt.SetColor("_Lit", new Color(1.5f, 1.35f, 1.05f));
            matVolt.SetColor("_Mid", new Color(1f, 0.6f, 0.12f));
            matVolt.SetColor("_Shade", new Color(0.95f, 0.32f, 0.04f));
            matVolt.SetColor("_InkColor", new Color(0.3f, 0.08f, 0.03f));
            matVolt.SetFloat("_Ink", 0.24f);

            var sun = RenderSettings.sun;
            if (sun == null)
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { sun = l; break; }
            if (sun != null) sunDir = -sun.transform.forward;
        }

        static Shader Find(string name, string resource)
        {
            var s = Shader.Find(name);
            return s != null ? s : Resources.Load<Shader>(resource);
        }

        Material Mat(Shader shader)
        {
            var m = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
            materials.Add(m);
            return m;
        }

        void DestroyToon()
        {
            foreach (var t in new Object[] { texCracks, texScorch, meshPuff }) if (t != null) Destroy(t);
        }

        /// <summary>A sphere 1 across made by splitting an icosahedron's faces <paramref name="splits"/> times (3 = 642
        /// points, evenly spread, no poles).</summary>
        static Mesh Icosphere(int splits)
        {
            float g = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var v = new List<Vector3>
            {
                new Vector3(-1f, g, 0f), new Vector3(1f, g, 0f), new Vector3(-1f, -g, 0f), new Vector3(1f, -g, 0f),
                new Vector3(0f, -1f, g), new Vector3(0f, 1f, g), new Vector3(0f, -1f, -g), new Vector3(0f, 1f, -g),
                new Vector3(g, 0f, -1f), new Vector3(g, 0f, 1f), new Vector3(-g, 0f, -1f), new Vector3(-g, 0f, 1f),
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            var f = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var middles = new Dictionary<long, int>();
            int Middle(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (middles.TryGetValue(key, out int m)) return m;
                v.Add(((v[a] + v[b]) * 0.5f).normalized);
                return middles[key] = v.Count - 1;
            }
            for (int s = 0; s < splits; s++)
            {
                var next = new List<int>(f.Count * 4);
                for (int i = 0; i < f.Count; i += 3)
                {
                    int a = f[i], b = f[i + 1], c = f[i + 2], ab = Middle(a, b), bc = Middle(b, c), ca = Middle(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                f = next;
            }
            var points = new Vector3[v.Count];
            for (int i = 0; i < v.Count; i++) points[i] = v[i] * 0.5f;
            var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = points;
            mesh.normals = v.ToArray();
            mesh.triangles = f.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The sun the puffs are shaded from (once a frame, cheap).</summary>
        void ShineToon() => Shader.SetGlobalVector("_FxSun", sunDir);

        // ---------------------------------------------------------------- a puff

        /// <summary>
        /// A cartoon puff: a lumpy sphere (<c>Skill Toon</c>) placed, grown, burnt and eaten away each frame by
        /// <see cref="animate"/> (given its age as a share of <see cref="life"/>).
        /// </summary>
        class Puff : Anim
        {
            readonly Transform tf;
            readonly MeshRenderer mr;
            readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
            public float life = 1f;
            public Vector3 at, scale = Vector3.one;
            public Quaternion rotation = Quaternion.identity;
            /// <summary>0 cold (smoke, wind), 1 all fire, above 1 white-hot.</summary>
            public float heat;
            public Color rim = Color.black;
            public float dissolve, lump = 0.3f;
            /// <summary>How fast its lumps boil (noise seconds per second).</summary>
            public float flow = 1f;
            public readonly Vector3 seed = new Vector3(Random.Range(0f, 90f), Random.Range(0f, 90f), Random.Range(0f, 90f));
            public Action<Puff, float> animate;
            public float Age => age;

            public Puff(Transform parent, string name, Mesh mesh, Material material)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                tf = go.transform;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.enabled = false;
            }

            public override bool Step(float dt, Camera cam)
            {
                age += dt;
                if (age >= life) return false;
                animate?.Invoke(this, age / life);
                bool show = dissolve < 0.99f && scale.sqrMagnitude > 1e-8f;
                mr.enabled = show;
                if (!show) return true;
                tf.SetPositionAndRotation(at, rotation);
                tf.localScale = scale;
                block.SetFloat("_Heat", heat);
                block.SetColor("_Rim", rim);
                block.SetFloat("_Dissolve", dissolve);
                block.SetFloat("_Lump", lump);
                block.SetVector("_Seed", new Vector4(seed.x, seed.y, seed.z, age * flow));
                mr.SetPropertyBlock(block);
                return true;
            }

            public override void Destroy() { if (tf != null) Object.Destroy(tf.gameObject); }
        }

        Puff Add(Puff p)
        {
            anims.Add(p);
            return p;
        }

        static float Smooth01(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

        /// <summary>A value that eases through the given (time, value) keys.</summary>
        static float Keys(float t, params float[] keys)
        {
            if (t <= keys[0]) return keys[1];
            for (int i = 2; i < keys.Length; i += 2)
                if (t <= keys[i])
                    return Mathf.Lerp(keys[i - 1], keys[i + 1], Smooth01((t - keys[i - 2]) / Mathf.Max(1e-4f, keys[i] - keys[i - 2])));
            return keys[keys.Length - 1];
        }

        /// <summary>
        /// Wind puffs bursting out from round <paramref name="c"/> (R86, the knight): <paramref name="count"/> puffs from
        /// radius <paramref name="from"/> to <paramref name="to"/> about <paramref name="axis"/>, lifting
        /// <paramref name="lift"/> along it, about <paramref name="size"/> across; flattened along the axis by
        /// <paramref name="flat"/>; eaten away over the back half of <paramref name="life"/>.
        /// </summary>
        void WindPuffs(Vector3 c, Vector3 axis, int count, float from, float to, float lift, float size, float life, float flat = 0.8f, float push = 0f, Vector3 pushDir = default)
        {
            axis = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.up;
            var e1 = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.forward).normalized;
            var e2 = Vector3.Cross(axis, e1);
            float turn = Random.Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < count; i++)
            {
                float a = turn + (i + Random.Range(-0.3f, 0.3f)) * Mathf.PI * 2f / count;
                Vector3 radial = e1 * Mathf.Cos(a) + e2 * Mathf.Sin(a);
                float reach = to * Random.Range(0.8f, 1.05f), up = lift * Random.Range(0.6f, 1.2f), s = size * Random.Range(0.75f, 1.2f);
                float l = life * Random.Range(0.85f, 1.15f), delay = Random.Range(0f, 0.03f);
                Vector3 drift = pushDir * push * Random.Range(0.6f, 1.2f);
                var rot = Quaternion.LookRotation(radial, axis);
                Add(new Puff(root, "Wind puff", meshPuff, matWind)
                {
                    life = l + delay,
                    lump = 0.38f,
                    flow = 1.4f,
                    rotation = rot,
                    animate = (p, t) =>
                    {
                        float age = Mathf.Max(0f, p.Age - delay), k = EaseOut(age / (l * 0.45f));
                        p.at = c + radial * Mathf.Lerp(from, reach, k) + axis * up * k + drift * k;
                        float grow = Mathf.Lerp(0.25f, 1f, EaseOut(age / (l * 0.3f)));
                        p.scale = new Vector3(s, s * flat, s * 1.15f) * grow * (p.Age < delay ? 0f : 1f);
                        p.dissolve = Keys(age / l, 0.25f, 0f, 0.85f, 1f);
                    },
                });
            }
        }

        // ---------------------------------------------------------------- cracks in the floor

        /// <summary>
        /// The queen's cracks (<c>Skill Crack</c>) lying flat round <see cref="at"/>, <see cref="radius"/> metres out:
        /// <see cref="reveal"/> runs them out from the middle, <see cref="cool"/> turns them from hot to scorched,
        /// <see cref="fade"/> takes them away. Placed by <see cref="animate"/> each frame.
        /// </summary>
        class Crack : Anim
        {
            readonly Transform tf;
            readonly MeshRenderer mr;
            readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
            public float life = 2f, radius = 2f, reveal, cool, fade = 1f, paint = 0.35f;
            public Color hot = Color.white;
            public Vector3 at;
            public Action<Crack, float> animate;
            public float Age => age;
            /// <summary>Its age when the blast came (below 0 before), and how far it had run out by then.</summary>
            public float BlastAt { get; private set; } = -1f;
            public float RevealAtBlast { get; private set; }
            public bool Gone { get; private set; }

            public void Blast()
            {
                BlastAt = age;
                RevealAtBlast = reveal;
            }

            public Crack(Transform parent, Mesh quad, Material material)
            {
                var go = new GameObject("Queen cracks");
                go.transform.SetParent(parent, false);
                tf = go.transform;
                tf.rotation = FlatOnFloor() * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                go.AddComponent<MeshFilter>().sharedMesh = quad;
                mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.enabled = false;
            }

            public override bool Step(float dt, Camera cam)
            {
                age += dt;
                if (age >= life) return false;
                animate?.Invoke(this, age / life);
                bool show = fade > 0.001f && reveal > 0.001f;
                mr.enabled = show;
                if (!show) return true;
                tf.position = at + Vector3.up * 0.02f;
                tf.localScale = Vector3.one * radius * 2f;
                block.SetFloat("_Reveal", reveal);
                block.SetFloat("_Cool", cool);
                block.SetFloat("_Fade", fade);
                block.SetFloat("_HotPaint", paint);
                block.SetColor("_Hot", hot);
                mr.SetPropertyBlock(block);
                return true;
            }

            public override void Destroy()
            {
                Gone = true;
                if (tf != null) Object.Destroy(tf.gameObject);
            }
        }

        /// <summary>
        /// Cracks radiating from the middle of a square texture, drawn by code: nine main cracks that wander out
        /// and branch, a few broken rings, a hot spot in the middle. R = the crack line, G = how far out along its
        /// crack (0..1, the queen's cracks run out in that order), B = a soft glow round the lines. Irregular on
        /// purpose: never a star (R78, R79).
        /// </summary>
        static Texture2D DrawCracks(int size, int seed)
        {
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            int n = size * size;
            var crack = new float[n];
            var glow = new float[n];
            var along = new float[n];
            for (int i = 0; i < n; i++) along[i] = 1f;
            float px = 2f / size;

            // One straight piece of a crack, a to b in -1..1 across the square, widths and "along" at both ends.
            void Piece(Vector2 a, Vector2 b, float wa, float wb, float da, float db)
            {
                const float reach = 0.1f;
                int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - reach + 1f) / px));
                int x1 = Mathf.Min(size - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + reach + 1f) / px));
                int y0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - reach + 1f) / px));
                int y1 = Mathf.Min(size - 1, Mathf.CeilToInt((Mathf.Max(a.y, b.y) + reach + 1f) / px));
                Vector2 ab = b - a;
                float len2 = Mathf.Max(1e-8f, ab.sqrMagnitude);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        var p = new Vector2((x + 0.5f) * px - 1f, (y + 0.5f) * px - 1f);
                        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                        float d = Vector2.Distance(p, a + ab * t);
                        float w = Mathf.Lerp(wa, wb, t) * 0.5f;
                        float line = 1f - Mathf.Clamp01((d - w) / px);
                        float halo = Mathf.Exp(-d / (w * 2f + 0.025f)) * 0.85f;
                        if (line <= 0.005f && halo <= 0.04f) continue;
                        int i = y * size + x;
                        crack[i] = Mathf.Max(crack[i], line);
                        glow[i] = Mathf.Max(glow[i], halo);
                        along[i] = Mathf.Min(along[i], Mathf.Lerp(da, db, t));
                    }
            }

            // Wander out from p heading `dir` for `length`, tapering from w0 to w1; may branch.
            void Wander(Vector2 p, float dir, float length, float w0, float w1, float start, float pull, int depth)
            {
                float gone = 0f, w = w0;
                while (gone < length)
                {
                    float step = R(0.035f, 0.07f);
                    float outward = Mathf.Atan2(p.y, p.x);
                    dir += R(-0.5f, 0.5f) + Mathf.DeltaAngle(dir * Mathf.Rad2Deg, outward * Mathf.Rad2Deg) * Mathf.Deg2Rad * pull;
                    var q = p + new Vector2(Mathf.Cos(dir), Mathf.Sin(dir)) * step;
                    float wn = Mathf.Lerp(w0, w1, Mathf.Clamp01((gone + step) / length));
                    Piece(p, q, w, wn, Mathf.Clamp01(start + gone), Mathf.Clamp01(start + gone + step));
                    if (depth < 2 && gone > 0.12f && rng.NextDouble() < 0.16)
                        Wander(q, dir + (rng.NextDouble() < 0.5 ? -1f : 1f) * R(0.5f, 0.95f), R(0.1f, 0.3f) * (depth == 0 ? 1f : 0.6f), wn * 0.8f, 0.003f, start + gone + step, 0.1f, depth + 1);
                    p = q;
                    gone += step;
                    w = wn;
                }
            }

            const int main = 9;
            float turn = R(0f, Mathf.PI * 2f);
            for (int k = 0; k < main; k++)
            {
                float a = turn + k * Mathf.PI * 2f / main + R(-0.32f, 0.32f);
                var start = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.05f;
                Wander(start, a, R(0.55f, 0.92f), R(0.018f, 0.026f), 0.004f, 0.05f, 0.25f, 0);
            }
            // Broken rings: short jagged arcs round the middle, each lighting up as the run reaches its radius.
            for (int k = 0; k < 6; k++)
            {
                float r = R(0.38f, 0.78f), a = R(0f, Mathf.PI * 2f), span = R(0.25f, 0.6f);
                var prev = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                int steps = Mathf.Max(3, Mathf.RoundToInt(span * r / 0.05f));
                for (int s = 1; s <= steps; s++)
                {
                    float b = a + span * s / steps, rr = r + R(-0.02f, 0.02f);
                    var next = new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * rr;
                    Piece(prev, next, 0.009f, 0.007f, r, r);
                    prev = next;
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, anisoLevel = 4, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color32[n];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    float u = (x + 0.5f) * px - 1f, v = (y + 0.5f) * px - 1f, r = Mathf.Sqrt(u * u + v * v);
                    // A hot spot in the middle where the blast started.
                    float spot = Mathf.Clamp01(1f - r / 0.1f);
                    float c = Mathf.Max(crack[i], spot * spot * 0.5f), g = Mathf.Max(glow[i], Mathf.Clamp01(1f - r / 0.22f) * 0.9f);
                    float d = r < 0.22f ? Mathf.Min(along[i], r) : along[i];
                    // Fade the outer edge so the quad's border never shows.
                    float edge = Mathf.Clamp01((1f - r) / 0.08f);
                    pixels[i] = new Color32((byte)(Mathf.Clamp01(c * edge) * 255f), (byte)(Mathf.Clamp01(d) * 255f), (byte)(Mathf.Clamp01(g * edge) * 255f), 255);
                }
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            return tex;
        }

        // ---------------------------------------------------------------- swooshes of wind

        /// <summary>
        /// A ribbon of wind (<c>Skill Swoosh</c>) along an arc round <see cref="center"/> about <see cref="axis"/>:
        /// <see cref="sweep"/> degrees from <see cref="startAngle"/>, rising <see cref="rise"/> metres along the axis
        /// (a swirl), its width lying along the radius (<see cref="tilt"/> 0, flat) or along the axis (1, a band).
        /// Only the part between <see cref="tail"/> and <see cref="head"/> shows, as a crescent. Rebuilt each frame.
        /// </summary>
        class Swoosh : Anim
        {
            const int Segments = 56;
            readonly Transform tf;
            readonly MeshRenderer mr;
            readonly Mesh mesh;
            readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
            readonly Vector3[] verts = new Vector3[(Segments + 1) * 2];
            readonly float seed = Random.Range(0f, 50f);
            public float life = 0.5f;
            public Func<Vector3> center;
            public Vector3 at, axis = Vector3.up;
            public float startAngle, sweep = 300f, radius = 1f, width = 0.2f, rise, tilt;
            public float head = 1f, tail, dissolve, fade = 1f;
            public Action<Swoosh, float> animate;
            public float Age => age;

            public Swoosh(Transform parent, Material material)
            {
                var go = new GameObject("Wind swoosh");
                go.transform.SetParent(parent, false);
                tf = go.transform;
                mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                mesh.MarkDynamic();
                var uv = new Vector2[verts.Length];
                var tris = new int[Segments * 6];
                for (int k = 0; k <= Segments; k++)
                {
                    float u = (float)k / Segments;
                    uv[k * 2] = new Vector2(u, 0f);
                    uv[k * 2 + 1] = new Vector2(u, 1f);
                    if (k < Segments)
                    {
                        int b = k * 2, t = k * 6;
                        tris[t] = b; tris[t + 1] = b + 1; tris[t + 2] = b + 2;
                        tris[t + 3] = b + 1; tris[t + 4] = b + 3; tris[t + 5] = b + 2;
                    }
                }
                mesh.vertices = verts;
                mesh.uv = uv;
                mesh.triangles = tris;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.enabled = false;
            }

            public override bool Step(float dt, Camera cam)
            {
                age += dt;
                if (age >= life) return false;
                animate?.Invoke(this, age / life);
                bool show = fade > 0.001f && head > tail + 0.001f && radius > 1e-3f;
                mr.enabled = show;
                if (!show) return true;
                Vector3 c = center != null ? center() : at;
                Vector3 a = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.up;
                var e1 = Vector3.Cross(a, Mathf.Abs(a.y) < 0.9f ? Vector3.up : Vector3.forward).normalized;
                var e2 = Vector3.Cross(a, e1);
                for (int k = 0; k <= Segments; k++)
                {
                    float u = (float)k / Segments, ang = (startAngle + sweep * u) * Mathf.Deg2Rad;
                    Vector3 radial = e1 * Mathf.Cos(ang) + e2 * Mathf.Sin(ang);
                    Vector3 p = radial * radius + a * (rise * u);
                    Vector3 across = Vector3.Lerp(radial, a, tilt).normalized * (width * 0.5f);
                    verts[k * 2] = p - across;
                    verts[k * 2 + 1] = p + across;
                }
                mesh.vertices = verts;
                mesh.RecalculateBounds();
                tf.position = c;
                block.SetFloat("_Head", head);
                block.SetFloat("_Tail", tail);
                block.SetFloat("_Dissolve", dissolve);
                block.SetFloat("_Fade", fade);
                block.SetFloat("_Seed", seed);
                mr.SetPropertyBlock(block);
                return true;
            }

            public override void Destroy()
            {
                if (tf != null) Object.Destroy(tf.gameObject);
                if (mesh != null) Object.Destroy(mesh);
            }
        }

        Swoosh Add(Swoosh s)
        {
            anims.Add(s);
            return s;
        }

        /// <summary>A crescent of wind that sweeps round <paramref name="c"/> (its head runs on, its tail follows) while
        /// it grows from <paramref name="from"/> to <paramref name="to"/> metres and thins out.</summary>
        void WindArc(Vector3 c, Vector3 axis, float from, float to, float width, float sweep, float life, float delay = 0f, float tilt = 0f, float rise = 0f, Func<Vector3> follow = null)
        {
            float start = Random.Range(0f, 360f), dirSign = Random.value < 0.5f ? -1f : 1f;
            Add(new Swoosh(root, matSwoosh)
            {
                life = life + delay,
                at = c,
                center = follow,
                axis = axis,
                sweep = sweep * dirSign,
                tilt = tilt,
                rise = rise,
                startAngle = start,
                animate = (s, t) =>
                {
                    float age = s.Age - delay;
                    if (age < 0f) { s.fade = 0f; return; }
                    float k = age / life;
                    s.fade = 1f;
                    s.radius = Mathf.Lerp(from, to, EaseOut(k * 1.6f));
                    s.width = Mathf.Lerp(width, width * 0.35f, k);
                    s.head = EaseOut(k * 3f);
                    s.tail = Smooth01((k - 0.12f) / 0.88f);
                    s.startAngle = start + dirSign * 90f * EaseOut(k);
                    s.dissolve = Keys(k, 0.45f, 0f, 1f, 0.9f);
                },
            });
        }

        // ---------------------------------------------------------------- lines of light

        /// <summary>A straight line of light from its <see cref="head"/> (bright) back to its <see cref="tail"/>, facing
        /// the camera, placed by <see cref="animate"/> each frame: speed lines and spindles.</summary>
        class Beam : Anim
        {
            readonly LineRenderer lr;
            readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
            public float life = 0.2f, width = 0.05f, bright = 1f, paint = -1f;
            public Vector3 head, tail;
            public Color color = Color.white;
            public Action<Beam, float> animate;
            public float Age => age;

            public Beam(Transform parent, Material material)
            {
                var go = new GameObject("Effect line");
                go.transform.SetParent(parent, false);
                lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = material;
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.alignment = LineAlignment.View;
                lr.textureMode = LineTextureMode.Stretch;
                lr.numCapVertices = 0;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.enabled = false;
            }

            public override bool Step(float dt, Camera cam)
            {
                age += dt;
                if (age >= life) return false;
                animate?.Invoke(this, age / life);
                bool show = bright > 0.001f && (head - tail).sqrMagnitude > 1e-6f;
                lr.enabled = show;
                if (!show) return true;
                lr.SetPosition(0, head);
                lr.SetPosition(1, tail);
                lr.widthMultiplier = width;
                block.SetColor("_Color", color * bright);   // the glow material
                block.SetFloat("_Fade", bright);            // the cartoon one (matVolt)
                if (paint >= 0f) block.SetFloat("_Opacity", paint);
                lr.SetPropertyBlock(block);
                return true;
            }

            public override void Destroy() { if (lr != null) Object.Destroy(lr.gameObject); }
        }

        Beam Add(Beam b)
        {
            anims.Add(b);
            return b;
        }

        /// <summary>A cartoon speed line (the rook's, <see cref="matVolt"/>): it shoots from <paramref name="at"/> along
        /// <paramref name="dir"/>, its head running out to <paramref name="length"/> and its tail catching up.</summary>
        void SpeedLine(Vector3 at, Vector3 dir, float length, float speed, float width, float life)
        {
            Add(new Beam(root, matVolt)
            {
                life = life,
                width = width,
                animate = (b, t) =>
                {
                    b.head = at + dir * (speed * b.Age + length * EaseOut(t * 2.5f));
                    b.tail = at + dir * (speed * b.Age + length * Smooth01((t - 0.2f) / 0.8f));
                    b.bright = 1f - t * t;
                },
            });
        }

        /// <summary>A line of light sucked in to <paramref name="target"/> from <paramref name="offset"/> away: its head
        /// races in, its tail follows, both there after <paramref name="life"/> seconds (the queen gathering).</summary>
        void InwardLine(Func<Vector3> target, Vector3 offset, float life, float width, Color color, float paint)
        {
            Add(new Beam(root, matBolt)
            {
                life = life,
                width = width,
                color = color,
                paint = paint,
                animate = (b, t) =>
                {
                    Vector3 c = target();
                    b.head = c + offset * (1f - Smooth01(t * 1.4f));
                    b.tail = c + offset * (1f - Smooth01(t * 0.9f));
                    b.bright = Mathf.Clamp01(t * 5f);
                },
            });
        }

        /// <summary>Cartoon speed lines bursting out of <paramref name="at"/> within <paramref name="cone"/> degrees of
        /// <paramref name="axis"/> (the rook's hits, R86 after the reference's electric dash).</summary>
        void LineBurst(Vector3 at, Vector3 axis, float cone, int count, float lengthMin, float lengthMax, float life = 0.16f)
        {
            for (int i = 0; i < count; i++)
            {
                var dir = cone >= 180f ? Random.onUnitSphere : InCone(axis, cone);
                SpeedLine(at + dir * Random.Range(0.05f, 0.25f), dir, Random.Range(lengthMin, lengthMax), Random.Range(3f, 7f),
                    Random.Range(0.09f, 0.15f), life * Random.Range(0.8f, 1.25f));
            }
        }

        /// <summary>
        /// Cartoon lightning that crawls along the path behind something moving (the rook's dash, R86 after the
        /// reference's electric streak): every few hundredths of a second, jagged lines re-drawn roughly along the
        /// last stretch of the path, white-hot in the middle, orange, inked at the edges (<see cref="matVolt"/>).
        /// </summary>
        class PathBolts : Anim
        {
            readonly LineRenderer[] lines;
            readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
            readonly Func<Vector3?> where;
            readonly List<Vector3> path = new List<Vector3>();
            readonly float stretch;
            float redraw, endedAt = -1f;

            public PathBolts(Transform parent, Material material, Func<Vector3?> where, float stretch)
            {
                this.where = where;
                this.stretch = stretch;
                lines = new LineRenderer[4];
                for (int i = 0; i < lines.Length; i++)
                {
                    var go = new GameObject("Effect path bolt");
                    go.transform.SetParent(parent, false);
                    var lr = go.AddComponent<LineRenderer>();
                    lr.sharedMaterial = material;
                    lr.positionCount = 8;
                    lr.useWorldSpace = true;
                    lr.numCapVertices = 0;
                    lr.alignment = LineAlignment.View;
                    lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    lr.receiveShadows = false;
                    lr.textureMode = LineTextureMode.Stretch;
                    lr.enabled = false;
                    lines[i] = lr;
                }
            }

            public override bool Step(float dt, Camera cam)
            {
                age += dt;
                var p = endedAt < 0f ? where() : null;
                if (p.HasValue) path.Add(p.Value);
                else if (endedAt < 0f) endedAt = age;
                float fade = endedAt < 0f ? 1f : 1f - (age - endedAt) / 0.15f;
                if (fade <= 0f) return false;
                // Only the last stretch of the path.
                float kept = 0f;
                int first = path.Count - 1;
                while (first > 0 && kept < stretch) { kept += Vector3.Distance(path[first], path[first - 1]); first--; }
                if (first > 0) path.RemoveRange(0, first);
                redraw -= dt;
                if (redraw <= 0f && path.Count >= 2)
                {
                    redraw = 0.035f;
                    foreach (var lr in lines)
                    {
                        float len = PathLength();
                        float a = Random.Range(0f, len * 0.5f), b = Mathf.Min(len, a + Random.Range(0.4f, 1f) * len);
                        int count = lr.positionCount;
                        Vector3 side = Random.onUnitSphere;
                        for (int k = 0; k < count; k++)
                        {
                            float u = Mathf.Lerp(a, b, k / (float)(count - 1));
                            Vector3 at = PointAt(u);
                            float swing = (k == 0 || k == count - 1) ? 0.03f : Random.Range(0.08f, 0.24f);
                            lr.SetPosition(k, at + (Random.onUnitSphere + side * 0.5f).normalized * swing);
                        }
                        lr.widthMultiplier = Random.Range(0.09f, 0.14f);
                        lr.enabled = true;
                    }
                }
                block.SetFloat("_Fade", fade);
                foreach (var lr in lines) lr.SetPropertyBlock(block);
                return true;
            }

            float PathLength()
            {
                float s = 0f;
                for (int i = 1; i < path.Count; i++) s += Vector3.Distance(path[i], path[i - 1]);
                return s;
            }

            /// <summary>The point <paramref name="back"/> metres back from the newest end of the path.</summary>
            Vector3 PointAt(float back)
            {
                for (int i = path.Count - 1; i > 0; i--)
                {
                    float d = Vector3.Distance(path[i], path[i - 1]);
                    if (back <= d) return Vector3.Lerp(path[i], path[i - 1], d > 1e-5f ? back / d : 0f);
                    back -= d;
                }
                return path[0];
            }

            public override void Destroy() { foreach (var lr in lines) if (lr != null) Object.Destroy(lr.gameObject); }
        }
    }
}
