using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.Rendering;
using Kit = ChessFight.RagdollLab.SkillInkKit;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// 10-25 (승규 님: "소드 파이트에 있는 스킬들을 모두 실사 버전으로 … 마법 없이 완전 실사로 … 완전 초퀄리티 실사 느낌",
    /// one piece at a time, the F skills first): what a real sword fight shows in place of design A's drawn pictures. No
    /// ink, no rings, no ribbons, no glow in a piece's colour: steel meeting steel throws hot sparks that light what is
    /// round them for a frame or two, light runs along a blade as it turns, a fast cut bends the air it goes through, a cut
    /// on a piece knocks chips of its own wood off it, and the hold, the slow beat and the shake carry the weight. The
    /// pieces drawn this way so far are in <see cref="Real"/>; the others keep design A until their turn.
    /// </summary>
    public partial class SwordFightSkillFx
    {
        /// <summary>The pieces whose F skill is drawn for real (10-25: the king first, then the queen, the rook, the bishop and
        /// the knight - all five now).</summary>
        public static bool Real(PieceKind kind) => kind == PieceKind.King || kind == PieceKind.Queen || kind == PieceKind.Rook || kind == PieceKind.Bishop || kind == PieceKind.Knight;

        SkillAir air;
        Material sparkMat;
        Mesh sparkMesh;

        SkillAir Air
        {
            get
            {
                if (air == null) air = new SkillAir(kit.root);
                return air;
            }
        }

        void StepReal()
        {
            air?.Step(dt);
            StepLensShock();
        }

        void DestroyReal()
        {
            air?.Destroy();
            air = null;
            if (sparkMat != null) Destroy(sparkMat);
            if (sparkMesh != null) Destroy(sparkMesh);
            if (blurMat != null) Destroy(blurMat);
            if (flashMat != null) Destroy(flashMat);
            if (glowTex != null) Destroy(glowTex);
            if (trailMat != null) Destroy(trailMat);
            if (ghostRealMat != null) Destroy(ghostRealMat);
            DestroyRealRookBishop();
            DestroyRealEdgeRBK();
        }

        /// <summary>The sparks' material: URP's unlit colour, added onto what is behind (hot metal gives light, it is not lit).</summary>
        Material SparkMaterial()
        {
            if (sparkMat != null) return sparkMat;
            sparkMat = new Material(LitMaterials.Unlit) { hideFlags = HideFlags.HideAndDontSave, name = "Real sparks" };
            LitMaterials.MakeTransparent(sparkMat, true);
            return sparkMat;
        }

        /// <summary>A unit streak along +z (a thin box from z 0 to 1, tapered to a point at the far end).</summary>
        Mesh SparkMesh()
        {
            if (sparkMesh != null) return sparkMesh;
            float w = 0.5f;
            var v = new[]
            {
                new Vector3(-w, -w, 0f), new Vector3(w, -w, 0f), new Vector3(w, w, 0f), new Vector3(-w, w, 0f),
                new Vector3(0f, 0f, 1f),
            };
            var t = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 4, 1, 2, 4, 2, 3, 4, 3, 0, 4 };
            sparkMesh = new Mesh { name = "Spark streak", hideFlags = HideFlags.HideAndDontSave, vertices = v, triangles = t };
            sparkMesh.RecalculateNormals();
            sparkMesh.RecalculateBounds();
            return sparkMesh;
        }

        /// <summary>The colour of steel this hot as it cools (<paramref name="k"/> 0 → 1): white-yellow, orange, dull red.</summary>
        static Color HotSteel(float k)
        {
            Color a = new Color(1f, 0.93f, 0.72f), b = new Color(1f, 0.55f, 0.16f), c = new Color(0.55f, 0.12f, 0.03f);
            return k < 0.4f ? Color.Lerp(a, b, k / 0.4f) : Color.Lerp(b, c, (k - 0.4f) / 0.6f);
        }

        /// <summary>
        /// <paramref name="n"/> sparks off steel struck at <paramref name="at"/>, thrown mostly along <paramref name="dir"/>:
        /// thin streaks as long as the way they go in a frame, cooling from white to orange to red as they fly, falling,
        /// skipping once off the board and dying there.
        /// </summary>
        void RealSparks(Vector3 at, Vector3 dir, int n, float speed, float spread = 0.7f, float heat = 1f)
        {
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.up;
            var mat = SparkMaterial();
            var mesh = SparkMesh();
            float floorY = Kit.FloorUnder(at, 2f).y;
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Spark", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(kit.root, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                Vector3 v = (dir + Random.insideUnitSphere * spread).normalized * speed * Random.Range(0.45f, 1.25f) + Vector3.up * Random.Range(0.2f, 1.4f);
                Vector3 p = at + Random.insideUnitSphere * 0.02f;
                // heat: hotter sparks (10-26) - a touch thicker, brighter and longer-lived.
                float life = Random.Range(0.2f, 0.55f) * Mathf.Lerp(1f, heat, 0.6f), width = Random.Range(0.009f, 0.018f) * heat;
                bool skipped = false;
                var block = new MaterialPropertyBlock();
                var f = kit.Run(life, (fx, d) =>
                {
                    if (go == null) return false;
                    v += Physics.gravity * d;
                    v *= Mathf.Exp(-1.2f * d);
                    Vector3 was = p;
                    p += v * d;
                    if (p.y < floorY + 0.005f && v.y < 0f)
                    {
                        if (skipped) return false;
                        skipped = true;
                        p.y = floorY + 0.005f;
                        v = new Vector3(v.x * 0.5f, -v.y * 0.3f, v.z * 0.5f);
                    }
                    float k = fx.age / fx.life;
                    // As long as its travel in a 1/30 s shutter (a fast spark is a streak, a slow one a dot), as a camera sees it.
                    float len = Mathf.Max(0.018f, v.magnitude / 30f);
                    Vector3 way = v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.up;
                    go.transform.SetPositionAndRotation(p - way * len, Quaternion.LookRotation(way));
                    float w = width * (1f - 0.6f * k);
                    go.transform.localScale = new Vector3(w, w, len);
                    Color c = HotSteel(k) * (Mathf.Lerp(3.2f, 0.6f, k) * heat);
                    c.a = 1f - k * k;
                    block.SetColor(LitMaterials.BaseColorId, c);
                    r.SetPropertyBlock(block);
                    return true;
                });
                f.end = () => { if (go != null) Destroy(go); };
            }
        }

        /// <summary>The light a burst of sparks throws for a moment: a warm point light at <paramref name="at"/>, bright at
        /// once and out in <paramref name="seconds"/>.</summary>
        void SparkLight(Vector3 at, float intensity, float seconds = 0.09f, float range = 2.6f, Color? color = null)
        {
            var go = new GameObject("Spark light");
            go.transform.SetParent(kit.root, false);
            go.transform.position = at;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color ?? new Color(1f, 0.78f, 0.5f);
            l.range = range;
            l.shadows = LightShadows.None;
            l.intensity = intensity;
            var f = kit.Run(seconds, (fx, d) =>
            {
                if (l == null) return false;
                float k = fx.age / fx.life;
                l.intensity = intensity * (1f - k) * (1f - k);
                return true;
            });
            f.end = () => { if (go != null) Destroy(go); };
        }

        /// <summary>
        /// Steel on steel at <paramref name="at"/> (10-25, 승규 님: "너무 실사 느낌이니까 조금 이펙트를 넣어줘도 돼", then "불꽃
        /// 말고 다른 걸로"): a film camera's look at a blinding point - a white flash there and a long thin streak of light
        /// across the picture through it (a lens streak, level on the screen), the air knocked out of the blow both ways
        /// (bent air, mostly back at whoever struck: <paramref name="back"/>), and a cold white light over everything near
        /// for two or three frames. No sparks, no rings.
        /// </summary>
        void SteelClash(Vector3 at, Vector3 back, float power = 1f)
        {
            ClashFlash(at, power);
            SparkLight(at, 1.2f * power, 0.08f, 1.6f, new Color(0.92f, 0.95f, 1f));
        }

        Material flashMat;
        Texture2D glowTex;

        /// <summary>A round soft glow: white in the middle, falling off as a gaussian to nothing at the edge.</summary>
        Texture2D GlowTexture()
        {
            if (glowTex != null) return glowTex;
            const int N = 64;
            glowTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, name = "Clash glow" };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float r2 = dx * dx + dy * dy;
                    float a = r2 >= 1f ? 0f : Mathf.Exp(-r2 * 5f) * (1f - r2);
                    px[y * N + x] = new Color(1f, 1f, 1f, a);
                }
            glowTex.SetPixels(px);
            glowTex.Apply();
            return glowTex;
        }

        Material FlashMaterial()
        {
            if (flashMat != null) return flashMat;
            flashMat = new Material(Shader.Find(LitMaterials.ParticlesUnlitName)) { hideFlags = HideFlags.HideAndDontSave, name = "Clash flash" };
            LitMaterials.MakeTransparent(flashMat, true);
            flashMat.SetTexture("_BaseMap", GlowTexture());
            flashMat.SetFloat("_Cull", 0f);
            return flashMat;
        }

        /// <summary>The flash and the lens streak of a blow at <paramref name="at"/>: both face the eye; the flash is gone in
        /// four frames, the streak (as wide as a piece is tall, a hair thin) in seven, thinning as it goes.</summary>
        void ClashFlash(Vector3 at, float power = 1f)
        {
            var quad = kit.meshQuad;
            var parts = new List<(Transform t, MeshRenderer r, MaterialPropertyBlock b)>();
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject(i == 0 ? "Clash flash" : "Clash streak", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(kit.root, false);
                go.GetComponent<MeshFilter>().sharedMesh = quad;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = FlashMaterial();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                parts.Add((go.transform, r, new MaterialPropertyBlock()));
            }
            var f = kit.Run(8f * F, (fx, d) =>
            {
                float a = fx.age / F;
                var cam = ViewCamera;
                if (cam == null) return true;
                Quaternion face = cam.transform.rotation;
                Vector3 toEye = (cam.transform.position - at).normalized;
                for (int i = 0; i < parts.Count; i++)
                {
                    var (t, r, b) = parts[i];
                    // Drawn a little toward the eye, so the pieces in the clash do not cut it.
                    t.SetPositionAndRotation(at + toEye * 0.15f, face);
                    float k, alpha;
                    Color c;
                    if (i == 0)
                    {
                        k = Mathf.Clamp01(a / 4f);
                        t.localScale = Vector3.one * (0.8f + 0.35f * k) * power;
                        alpha = (1f - k) * (1f - k);
                        c = new Color(1f, 0.97f, 0.92f) * 3.2f;
                    }
                    else
                    {
                        // Two streaks: a long faint one and a short bright core.
                        k = Mathf.Clamp01(a / 7f);
                        float len = (i == 1 ? 2.8f : 1.4f) * power * (0.8f + 0.2f * k), thick = (i == 1 ? 0.05f : 0.022f) * (1f - 0.7f * k);
                        t.localScale = new Vector3(len, thick, 1f);
                        alpha = (1f - k) * (i == 1 ? 0.55f : 1f);
                        c = new Color(0.88f, 0.94f, 1f) * (i == 1 ? 2.2f : 3.6f);
                    }
                    c.a = alpha;
                    b.SetColor(LitMaterials.BaseColorId, c);
                    r.SetPropertyBlock(b);
                }
                return true;
            });
            f.end = () => { foreach (var p in parts) if (p.t != null) Destroy(p.t.gameObject); };
        }

        /// <summary>Where on <paramref name="f"/>'s blade a blow at <paramref name="p"/> meets it (the nearest point of the
        /// blade, root to tip); <paramref name="p"/> itself with no sword.</summary>
        static Vector3 OnBlade(SwordFightPawn f, Vector3 p)
        {
            if (f == null || f.SwordBody == null) return p;
            Vector3 a = f.BladeRoot, b = f.BladeTip, ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return a + ab * Mathf.Max(0.25f, t);
        }

        /// <summary>Light running along a blade as it turns (a real blade catching the sun): a thin bright line sliding
        /// from the guard to the tip over <paramref name="seconds"/>, on the edge facing the eye.</summary>
        void BladeGlint(SwordFightPawn f, float seconds = 0.22f, float strength = 1f)
        {
            if (f == null || f.SwordBody == null) return;
            var go = new GameObject("Blade glint", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(kit.root, false);
            go.GetComponent<MeshFilter>().sharedMesh = SparkMesh();
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = SparkMaterial();
            r.shadowCastingMode = ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            var run = kit.Run(seconds, (fx, d) =>
            {
                if (go == null || f == null || f.SwordBody == null) return false;
                float k = fx.age / fx.life;
                Vector3 a = f.BladeRoot, b = f.BladeTip, along = b - a;
                float len = along.magnitude * 0.22f;
                Vector3 way = along.normalized;
                Vector3 at = Vector3.Lerp(a, b, Mathf.Lerp(0.05f, 0.95f, k * k * (3f - 2f * k))) - way * len * 0.5f;
                // A hair off the blade toward the eye, so the line lies on its face, not inside it.
                at += (Eye - at).normalized * 0.012f;
                go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(way));
                go.transform.localScale = new Vector3(0.006f, 0.006f, len);
                Color c = new Color(1f, 0.97f, 0.9f) * 2.4f * strength;
                c.a = Mathf.Sin(Mathf.PI * k);
                block.SetColor(LitMaterials.BaseColorId, c);
                r.SetPropertyBlock(block);
                return true;
            });
            run.end = () => { if (go != null) Destroy(go); };
        }

        Material blurMat;

        /// <summary>
        /// A fast blade's motion blur, as a camera's shutter sees it: the sheet the blade sweeps through over its last few
        /// frames, steel-grey and see-through, densest where the blade is now and fading out behind it and toward the hilt
        /// (the tip goes fastest). Kept up for <paramref name="seconds"/>, then let fade as the blade slows.
        /// </summary>
        void BladeBlur(SwordFightPawn f, float seconds, float strength = 1f)
        {
            if (f == null || f.SwordBody == null) return;
            if (blurMat == null)
            {
                blurMat = new Material(Shader.Find(LitMaterials.ParticlesUnlitName)) { hideFlags = HideFlags.HideAndDontSave, name = "Blade blur" };
                LitMaterials.MakeTransparent(blurMat);
                blurMat.SetColor(LitMaterials.BaseColorId, Color.white);
            }
            var go = new GameObject("Blade blur", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(kit.root, false);
            var mesh = new Mesh { name = "Blade blur", hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = blurMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            const int Keep = 7, Along = 6;
            var roots = new List<Vector3>();
            var tips = new List<Vector3>();
            var v = new List<Vector3>();
            var c = new List<Color>();
            var t = new List<int>();
            // The blade as the light shows it: a pale steel, a little warm from the lamp.
            Color steel = new Color(0.93f, 0.95f, 1f);
            // 10-25 (승규 님: "조금 이펙트를 넣어줘도 돼"): a bright edge on the blur where the tip went - the trail an action
            // film's blade leaves, thin and white, gone with the blur.
            var edge = new GameObject("Blade trail", typeof(MeshFilter), typeof(MeshRenderer));
            edge.transform.SetParent(kit.root, false);
            var edgeMesh = new Mesh { name = "Blade trail", hideFlags = HideFlags.HideAndDontSave };
            edgeMesh.MarkDynamic();
            edge.GetComponent<MeshFilter>().sharedMesh = edgeMesh;
            var er = edge.GetComponent<MeshRenderer>();
            er.sharedMaterial = TrailMaterial();
            er.shadowCastingMode = ShadowCastingMode.Off;
            er.receiveShadows = false;
            var run = kit.Run(seconds + 6f * F, (fx, d) =>
            {
                if (f == null || f.SwordBody == null) return false;
                if (fx.age < seconds) { roots.Add(f.BladeRoot); tips.Add(f.BladeTip); }
                else if (roots.Count > 0) { roots.RemoveAt(0); tips.RemoveAt(0); }
                while (roots.Count > Keep) { roots.RemoveAt(0); tips.RemoveAt(0); }
                v.Clear(); c.Clear(); t.Clear();
                int n = roots.Count;
                if (n >= 2)
                {
                    for (int i = 0; i < n; i++)
                    {
                        float age = (n - 1 - i) / (float)(Keep - 1);   // 0 now, 1 the oldest kept
                        for (int j = 0; j <= Along; j++)
                        {
                            float u = j / (float)Along;   // 0 hilt, 1 tip
                            v.Add(Vector3.Lerp(roots[i], tips[i], u));
                            float a = 0.2f * strength * (1f - age) * (1f - age) * Mathf.Lerp(0.05f, 1f, u * u * u);
                            c.Add(new Color(steel.r, steel.g, steel.b, a));
                        }
                    }
                    int row = Along + 1;
                    for (int i = 0; i < n - 1; i++)
                        for (int j = 0; j < Along; j++)
                        {
                            int a0 = i * row + j, b0 = a0 + row;
                            t.AddRange(new[] { a0, b0, a0 + 1, a0 + 1, b0, b0 + 1, a0, a0 + 1, b0, a0 + 1, b0 + 1, b0 });   // both faces
                        }
                }
                mesh.Clear();
                mesh.SetVertices(v);
                mesh.SetColors(c);
                mesh.SetTriangles(t, 0);
                mesh.RecalculateBounds();
                BuildTrailEdge(edgeMesh, roots, tips, Keep, strength);
                return true;
            });
            run.end = () => { if (go != null) Destroy(go); Destroy(mesh); if (edge != null) Destroy(edge); Destroy(edgeMesh); };
        }

        Material slashMat;

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }

        /// <summary>
        /// 10-26 (승규 님: "킹 칼 휘두르는 vfx를 멋있는 걸로"): the king's counter cut leaves a crescent of gold light where
        /// the outer part of his blade went - brightest along the path of the edge (with the blade trail's white line over
        /// it), fading toward the hilt and just past the edge, smoothed through the frames so the arc is one clean sweep. It lingers a moment behind the blade,
        /// then burns away from the inside out toward the edge.
        /// </summary>
        void KingSlash(SwordFightPawn f, float seconds)
        {
            if (f == null || f.SwordBody == null) return;
            if (slashMat == null)
            {
                slashMat = new Material(Shader.Find(LitMaterials.ParticlesUnlitName)) { hideFlags = HideFlags.HideAndDontSave, name = "King slash" };
                LitMaterials.MakeTransparent(slashMat, true);
                slashMat.SetFloat("_Cull", 0f);
                // The gold is the material's own (this shader takes only the alpha of the vertex colours); the white-hot core
                // along the edge is the blade trail's white line drawn over it.
                // Kept near 1 in gold proportion: past it red and green both clip and the gold turns lime.
                slashMat.SetColor(LitMaterials.BaseColorId, new Color(1f, 0.64f, 0.17f, 1f));
            }
            var go = new GameObject("King slash", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(kit.root, false);
            var mesh = new Mesh { name = "King slash", hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = slashMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            const float Hold = 0.26f, Burn = 0.22f;
            const int Sub = 4;
            // Across the sweep, from the inside out: where on the blade (0 hilt, 1 tip, past 1 beyond the edge) and how bright.
            float[] across = { 0.6f, 0.8f, 0.92f, 0.985f, 1.0f, 1.04f, 1.15f };
            float[] lit = { 0f, 0.35f, 0.8f, 1f, 1f, 0.65f, 0f };
            var roots = new List<Vector3>();
            var tips = new List<Vector3>();
            var times = new List<float>();
            var v = new List<Vector3>();
            var c = new List<Color>();
            var t = new List<int>();
            var run = kit.Run(seconds + Hold + Burn, (fx, d) =>
            {
                if (f == null || f.SwordBody == null) return false;
                if (fx.age <= seconds)
                {
                    Vector3 tip = f.BladeTip, root = f.BladeRoot;
                    if (tips.Count == 0 || (tip - tips[tips.Count - 1]).sqrMagnitude > 1e-5f)
                    {
                        roots.Add(root);
                        tips.Add(tip);
                        times.Add(fx.age);
                    }
                }
                v.Clear(); c.Clear(); t.Clear();
                int n = tips.Count;
                // Burning away once the blade has stopped: the inner edge moves out to the outer, all of it dimming.
                float burn = Mathf.Clamp01((fx.age - seconds - Hold * 0.5f) / Burn);
                float inner = Mathf.Lerp(0f, 0.85f, burn * burn);
                if (n >= 2)
                {
                    int rows = across.Length, cols = 0;
                    for (int i = 0; i < n - 1; i++)
                    {
                        Vector3 r0 = roots[Mathf.Max(0, i - 1)], r1 = roots[i], r2 = roots[i + 1], r3 = roots[Mathf.Min(n - 1, i + 2)];
                        Vector3 q0 = tips[Mathf.Max(0, i - 1)], q1 = tips[i], q2 = tips[i + 1], q3 = tips[Mathf.Min(n - 1, i + 2)];
                        int last = i == n - 2 ? Sub : Sub - 1;
                        for (int k = 0; k <= last; k++)
                        {
                            float u = k / (float)Sub;
                            Vector3 rt = CatmullRom(r0, r1, r2, r3, u), tp = CatmullRom(q0, q1, q2, q3, u);
                            float when = Mathf.Lerp(times[i], times[i + 1], u);
                            float age = fx.age - when;
                            // The newest is where the blade is; behind it the arc stays lit for a moment, then dims.
                            float along = Mathf.Clamp01(1f - age / (Hold + seconds * 0.5f));
                            along *= along;
                            // The first of the sweep thins in, so the crescent has a point at its start.
                            float start = Mathf.Clamp01((when - times[0]) / Mathf.Max(0.02f, seconds * 0.25f));
                            float a = along * Mathf.SmoothStep(0f, 1f, start) * (1f - burn);
                            Vector3 blade = tp - rt;
                            for (int j = 0; j < rows; j++)
                            {
                                float x = Mathf.Max(across[j], Mathf.Lerp(across[0], 0.99f, inner));
                                if (j == rows - 1) x = across[j];
                                v.Add(rt + blade * x);
                                c.Add(new Color(1f, 1f, 1f, a * lit[j]));
                            }
                            cols++;
                        }
                    }
                    for (int i = 0; i < cols - 1; i++)
                        for (int j = 0; j < rows - 1; j++)
                        {
                            int a0 = i * rows + j, b0 = a0 + rows;
                            t.Add(a0); t.Add(b0); t.Add(a0 + 1);
                            t.Add(a0 + 1); t.Add(b0); t.Add(b0 + 1);
                        }
                }
                mesh.Clear();
                mesh.SetVertices(v);
                mesh.SetColors(c);
                mesh.SetTriangles(t, 0);
                mesh.RecalculateBounds();
                return true;
            });
            run.end = () => { if (go != null) Destroy(go); Destroy(mesh); };
        }

        Material trailMat;

        Material TrailMaterial()
        {
            if (trailMat != null) return trailMat;
            trailMat = new Material(Shader.Find(LitMaterials.ParticlesUnlitName)) { hideFlags = HideFlags.HideAndDontSave, name = "Blade trail" };
            LitMaterials.MakeTransparent(trailMat, true);
            trailMat.SetFloat("_Cull", 0f);
            trailMat.SetColor(LitMaterials.BaseColorId, new Color(2.2f, 2.25f, 2.4f, 1f));
            return trailMat;
        }

        /// <summary>The trail's ribbon along the path of the tip, turned to face the eye (so a straight thrust, whose blade
        /// slides along itself, shows it as well as a cut): bright down its middle, soft at its two edges, brightest and
        /// widest at the newest.</summary>
        void BuildTrailEdge(Mesh m, List<Vector3> roots, List<Vector3> tips, int keep, float strength)
        {
            var v = new List<Vector3>();
            var c = new List<Color>();
            var t = new List<int>();
            int n = tips.Count;
            Vector3 eye = Eye;
            for (int i = 0; i < n; i++)
            {
                float age = (n - 1 - i) / (float)(keep - 1);
                float a = strength * (1f - age) * (1f - age);
                Vector3 along = tips[Mathf.Min(n - 1, i + 1)] - tips[Mathf.Max(0, i - 1)];
                if (along.sqrMagnitude < 1e-8f) along = tips[i] - roots[i];
                Vector3 across = Vector3.Cross(along, eye - tips[i]);
                across = across.sqrMagnitude > 1e-8f ? across.normalized : Vector3.up;
                float w = 0.018f * (1f - 0.6f * age);
                v.Add(tips[i] + across * w);
                v.Add(tips[i]);
                v.Add(tips[i] - across * w);
                c.Add(new Color(1f, 1f, 1f, 0f));
                c.Add(new Color(1f, 1f, 1f, a));
                c.Add(new Color(1f, 1f, 1f, 0f));
            }
            for (int i = 0; i < n - 1; i++)
            {
                int a0 = i * 3, b0 = a0 + 3;
                t.AddRange(new[] { a0, b0, a0 + 1, a0 + 1, b0, b0 + 1, a0 + 1, b0 + 1, a0 + 2, a0 + 2, b0 + 1, b0 + 2 });
            }
            m.Clear();
            m.SetVertices(v);
            m.SetColors(c);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
        }

        Material ghostRealMat;

        /// <summary>The skin the piece is drawn with now (its model), for a copy of it.</summary>
        static SkinnedMeshRenderer DrawnSkin(RagdollPawn pawn)
        {
            if (pawn == null) return null;
            if (pawn.skin != null && pawn.skin.enabled && pawn.skin.gameObject.activeInHierarchy) return pawn.skin;
            foreach (var s in pawn.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (s.enabled && s.gameObject.activeInHierarchy) return s;
            return pawn.skin;
        }

        /// <summary>
        /// 10-25 (승규 님, the queen: "잔상이 남아진 상태 (잔상을 좀 더 사실적으로 묘사 할 것)"): an afterimage as a long
        /// exposure leaves one - the piece itself, its own wood and lit as it is, held where it was this frame and
        /// see-through, fading in <paramref name="life"/> seconds, drawn a little back along <paramref name="back"/> as it
        /// goes. No colour of its own, no outline, no swelling.
        /// </summary>
        void RealGhost(RagdollPawn pawn, float alpha, float life, Vector3 back, Vector3 shift = default)
        {
            var skin = DrawnSkin(pawn);
            if (skin == null) return;
            if (ghostRealMat == null)
            {
                ghostRealMat = LitMaterials.NewLit(Color.white, 0.25f);
                ghostRealMat.hideFlags = HideFlags.HideAndDontSave;
                ghostRealMat.name = "Real afterimage";
                LitMaterials.MakeTransparent(ghostRealMat);
            }
            var mesh = new Mesh { name = "Real afterimage", hideFlags = HideFlags.HideAndDontSave };
            skin.BakeMesh(mesh, true);
            var go = new GameObject("Real afterimage", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(kit.root, false);
            Vector3 pos = skin.transform.position + shift;   // shift: a copy left where it was a moment ago
            go.transform.SetPositionAndRotation(pos, skin.transform.rotation);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = ghostRealMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            var src = skin.sharedMaterial;
            Color body = src != null && src.HasProperty(LitMaterials.BaseColorId) ? src.GetColor(LitMaterials.BaseColorId) : Color.white;
            Texture map = src != null && src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : null;
            if (map != null) block.SetTexture("_BaseMap", map);
            var f = kit.Run(life, (fx, d) =>
            {
                if (go == null) return false;
                float k = fx.age / fx.life;
                go.transform.position = pos + back * (0.12f * k);
                // A touch paler than the piece (light through it), fading out.
                Color c = Color.Lerp(body, new Color(0.9f, 0.92f, 0.96f), 0.15f);
                c.a = alpha * (1f - k) * (1f - k);
                block.SetColor(LitMaterials.BaseColorId, c);
                r.SetPropertyBlock(block);
                return true;
            });
            f.end = () => { if (go != null) Destroy(go); Destroy(mesh); };
        }

        // ---------------------------------------------------------------- the queen, for real

        /// <summary>
        /// The thrust (10-25, 승규 님: "잔상이 남아진 상태 … 칼날의 움직임 흐름과 공기의 일렁임, 맞는 순간 불꽃은 하지마"): she
        /// pushes off with the air knocked out behind her; while she runs, a see-through copy of her is left every other
        /// frame (<see cref="RealGhost"/>), her blade blurs ahead with its bright trail, and the air she tears through bends
        /// behind her; her base scuffs the board (<see cref="FloorScuff"/>). A hold, a kick of the view, a shake - no gold.
        /// </summary>
        void RealQueenDash(SfFxEvent e)
        {
            var s = e.by;
            var pawn = s.Pawn;
            if (pawn == null) return;
            Vector3 start = Kit.FloorUnder(e.at, 1.5f), dir = Kit.FlatDir(e.dir, Vector3.forward);
            Air.Kick(pawn.Hips.position - dir * 0.3f, -dir + Vector3.down * 0.25f, 1.3f);
            HitStop(2);
            Shake(pawn, null, 0.08f, 6);
            Punch(pawn, null, 6f, 16);
            FloorScuff(s, start, dir);
            var S = this.S;
            float runFor = S != null ? s.Reach / Mathf.Max(1f, S.queenDashSpeed) + 0.1f : 0.6f;
            BladeBlur(s.Fighter, runFor, 1f);
            BladeGlint(s.Fighter, 0.2f, 1f);
            // 10-26 (승규 님: "퀸이 F스킬을 쓸 때 검에 불꽃 파티클"): her blade throws sparks as it goes - a burst off its edge
            // as she lets go, then a stream from along it, most from near the tip, flung back past her and up, and a warm
            // light riding the tip.
            var fighter = s.Fighter;
            if (fighter != null && fighter.SwordBody != null)
            {
                Vector3 tip0 = fighter.BladeTip;
                RealSparks(tip0, -dir + Vector3.up * 0.6f, 14, 4.6f, 0.8f, 1.25f);
                SparkLight(tip0, 1.8f, 0.1f, 2f);
            }
            var tipLightGo = new GameObject("Queen blade light");
            tipLightGo.transform.SetParent(kit.root, false);
            var tipLight = tipLightGo.AddComponent<Light>();
            tipLight.type = LightType.Point;
            tipLight.color = new Color(1f, 0.7f, 0.38f);
            tipLight.range = 1.6f;
            tipLight.shadows = LightShadows.None;
            tipLight.intensity = 0f;
            float nextGhost = 0f, nextAir = 0.06f, nextSpark = 0f;
            var dash = kit.Run(2f, (fx, d) =>
            {
                bool dashing = s != null && pawn != null && s.Stage == SfStage.Active && s.Piece == PieceKind.Queen;
                if (!dashing) return false;
                if (fx.age >= nextGhost)
                {
                    RealGhost(pawn, 0.32f, 0.2f, -dir);
                    nextGhost = fx.age + 2f * F;
                }
                if (fx.age >= nextAir)
                {
                    Air.Kick(pawn.bodies[(int)BodyId.Chest].position - dir * 0.35f, -dir, 0.7f);
                    nextAir = fx.age + 0.09f;
                }
                if (fighter != null && fighter.SwordBody != null)
                {
                    Vector3 root = fighter.BladeRoot, tip = fighter.BladeTip;
                    if (fx.age >= nextSpark)
                    {
                        nextSpark = fx.age + F;
                        for (int i = 0; i < 2; i++)
                        {
                            float u = Mathf.Lerp(0.4f, 1f, Mathf.Sqrt(Random.value));
                            RealSparks(Vector3.Lerp(root, tip, u), -dir + Vector3.up * 0.35f, 2, 3.8f, 0.6f, 1.15f);
                        }
                    }
                    if (tipLight != null)
                    {
                        tipLight.transform.position = tip;
                        tipLight.intensity = 1.3f * (0.75f + 0.25f * Mathf.Sin(kit.Clock * 70f));
                    }
                }
                return true;
            });
            dash.end = () => { if (tipLightGo != null) Destroy(tipLightGo); };
        }

        /// <summary>
        /// Her way while she aims and winds up, for real (it was gold squares): two faint lines of light lying on the board
        /// at the edges of what her thrust takes and a fainter one down its middle, out to its reach, as a light catching
        /// the grain would lie. Brighter as the thrust comes; her own aim is fainter than the windup everyone sees.
        /// </summary>
        void RealLane(SwordFightSkills s, SwordFightSkillParams S, bool aim, float alpha)
        {
            var owner = s;
            LightLane(s, "queen real lane", alpha, () =>
            {
                bool aiming = owner.Stage == SfStage.Aim;
                return (Kit.FloorUnder(owner.Pawn.Hips.position, 0.6f), aiming ? owner.AimDir : owner.Dir, 0.3f, aiming ? owner.AimReach : owner.Reach,
                        (S != null ? S.queenWidth : 1.2f) * 0.5f, aiming ? 0.3f : Kit.Charge(owner.StageTime, S != null ? S.queenWindup : 0.4f));
            });
        }

        /// <summary>She cuts through someone on her way: chips of their wood off the cut, the air knocked on past them the
        /// way she goes, a hold, a shake (no sparks).</summary>
        void RealQueenHit(SfFxEvent e)
        {
            if (e.target == null) return;
            // 10-26 (승규 님: "적이 맞으면 어울리는 임펙트"): the sparking blade goes into the body - where it meets it (not
            // the point between them), a white flash and its lens streak, a burst of its sparks thrown on through and off
            // the body, a hot light for a moment, the cut left on the piece, the air knocked on, chips of its wood.
            bool first = e.order == 0;
            Vector3 from = e.by != null && e.by.Pawn != null ? e.by.Pawn.Hips.position : e.at - e.dir;
            Vector3 at = OnBody(e.target, from, out Vector3 normal);
            ClashFlash(at + normal * 0.04f, first ? 1f : 0.75f);
            RealSparks(at, e.dir + Vector3.up * 0.35f, first ? 20 : 12, 5.2f, 0.6f, 1.25f);
            RealSparks(at, normal + Vector3.up * 0.5f, first ? 8 : 5, 2.6f, 0.7f, 1.1f);
            SparkLight(at + normal * 0.1f, first ? 2.6f : 1.8f, 0.12f, 2.4f);
            CutMark(e.target, at, normal, e.dir);
            Air.Kick(e.target.bodies[(int)BodyId.Chest].position, e.by.Dir, first ? 1.1f : 0.8f);
            var real = e;
            real.at = at;
            RealHit(real, first ? 5 : 3, first ? 0.15f : 0.1f);
        }

        /// <summary>F for a piece drawn for real: light runs down the blade as it is set, and a small kick of the view.</summary>
        void RealCast(SfFxEvent e)
        {
            var f = e.by.Fighter;
            kit.Run(4f * F, (fx, d) =>
            {
                if (fx.age < 3f * F) return true;
                BladeGlint(f, 0.24f, 1f);
                return false;
            });
            Punch(e.by.Pawn, null, 1.5f, 10);
        }

        /// <summary>The colour of a piece's body (its model's material), for the chips a cut knocks off it.</summary>
        static Color BodyColor(RagdollPawn pawn)
        {
            var skin = pawn != null ? pawn.GetComponentInChildren<SkinnedMeshRenderer>() : null;
            if (skin != null && skin.sharedMaterial != null && skin.sharedMaterial.HasProperty(LitMaterials.BaseColorId))
                return skin.sharedMaterial.GetColor(LitMaterials.BaseColorId);
            return pawn != null && pawn.Team == ChessFight.Gameplay.Teams.Black ? new Color(0.17f, 0.15f, 0.13f) : new Color(0.93f, 0.9f, 0.84f);
        }

        /// <summary>A sword's cut landing on a piece for real: chips of its own wood knocked off where it was struck and
        /// thrown on the way it goes, a hold, a shake and a kick of the view. No flash, no ring, nothing in a colour.</summary>
        void RealHit(SfFxEvent e, int stopFrames, float shakeMetres)
        {
            if (e.target == null) return;
            var keepA = chipA; var keepB = chipB; float keepShare = chipShare;
            Color c = BodyColor(e.target);
            chipA = ChipsOf(c); chipB = ChipsOf(c, 0.75f); chipShare = 0.6f;
            WallChips(e.at, e.dir + Vector3.up * 0.5f, 10, 3.2f, 0.6f, 0.008f, 0.03f);
            chipA = keepA; chipB = keepB; chipShare = keepShare;
            HitStop(Mathf.Max(stopFrames, 3));
            Shake(e.by.Pawn, e.target, shakeMetres, 7);
            Punch(e.by.Pawn, e.target, 2.5f, 10);
        }

        // ---------------------------------------------------------------- the king, for real

        /// <summary>F: he sets himself, blade across his front; light runs down it. Nothing else shows: the stance is
        /// the warning.</summary>
        static readonly Color KingGold = new Color(1f, 0.76f, 0.38f);

        void RealKingCast(SfFxEvent e)
        {
            var f = e.by.Fighter;
            kit.Run(4f * F, (fx, d) =>
            {
                // The blade is turned into the guard over these frames: the glint once it is there.
                if (fx.age < 3f * F) return true;
                BladeGlint(f, 0.24f, 1f);
                return false;
            });
            Punch(e.by.Pawn, null, 1.5f, 10);
            // 10-26 (승규 님: "왕의 반격 이펙트가 밋밋해"): he plants himself - the air at his feet is pushed out
            // (bent air only, four ways) and a warm gold light comes up on him for the guard.
            var king = e.by.Pawn;
            if (king != null)
            {
                Vector3 feet = Kit.FloorUnder(king.Hips.position, 1.5f) + Vector3.up * 0.12f, fwd = Kit.FlatDir(king.Facing, Vector3.forward);
                Vector3 side = Vector3.Cross(Vector3.up, fwd);
                foreach (var w in new[] { fwd, -fwd, side, -side })
                    Air.Kick(feet + w * 0.2f, w + Vector3.down * 0.1f, 0.9f, null, 0.04f, 1.6f, 1.4f);
                SparkLight(king.bodies[(int)BodyId.Chest].position + fwd * 0.4f, 1.6f, 0.45f, 2.4f, KingGold);
            }
        }

        /// <summary>
        /// The block: his blade takes the blow where it meets it, steel on steel - a white flash and a lens streak there
        /// and a cold light for a moment (<see cref="SteelClash"/>, no sparks), a hold, a short slow beat, a shake. The
        /// bishop's hands are cut through at the wrist as they reach his ankles (a flash there).
        /// </summary>
        void RealKingBlock(SfFxEvent e)
        {
            var s = e.by;
            var king = s.Pawn;
            if (king == null) return;
            var kind = (SfBlock)e.order;
            var attacker = e.target;
            if (kind == SfBlock.Bishop)
            {
                var bishop = attacker != null ? attacker.GetComponent<SwordFightSkills>() : null;
                if (bishop != null) handsCut[bishop] = kit.Clock;
                if (Real(PieceKind.Bishop)) DeflectStakes(bishop, king);
                else CutHands(bishop, king);
                return;
            }
            Vector3 toward = Kit.FlatDir(e.dir, king.Facing);
            Vector3 at = OnBlade(s.Fighter, e.at);
            SteelClash(at, toward, kind == SfBlock.Sword ? 1f : 1.3f);
            // 10-26: the blow stopped dead throws the air back at the attacker (bent air), the lens shudders and the
            // king's gold flares at the meeting point.
            Air.Kick(at, toward, 1.5f, null, 0.06f, 2.2f, 1.5f);
            Air.Kick(at, Vector3.Slerp(toward, Vector3.up, 0.45f), 1.1f, null, 0.05f, 1.6f, 1.2f);
            SparkLight(at, 3f, 0.22f, 3f, KingGold);
            LensShock(king, attacker, 0.75f, 0.2f);
            RealBlockBeat(king, attacker);
        }

        /// <summary>What a block does once it lands, for real: a hold, a short slow beat (online: a hold only), the view
        /// pulled to the two of them, a shake. No flash, no afterimage, no colour.</summary>
        void RealBlockBeat(RagdollPawn king, RagdollPawn attacker)
        {
            HitStop(6, king, attacker);
            if (Online) HitStop(OnlineBeatFrames, king, attacker);
            else SlowMo(king, attacker, 0.16f, 0.4f);
            Pull(king, attacker, 5f, 0.16f);
            Shake(king, attacker, 0.11f, 8);
            Punch(king, attacker, 2f, 10);
        }

        /// <summary>
        /// The counter: one big diagonal cut at whoever he blocked (10-25, it was a full turn round him). The blade's
        /// motion blur and its bright trail, light running down it, and the air it cuts thrown out ahead of it as it goes
        /// (bent air, nothing drawn); a short hold and a shake. Whoever it reaches is hit for real (<see cref="RealHit"/>).
        /// </summary>
        void RealKingSpin(SfFxEvent e)
        {
            var s = e.by;
            var king = s.Pawn;
            if (king == null) return;
            var S = this.S;
            float cut = S != null ? S.kingCounterTime : 0.34f;
            // The grey blur is kept faint under the gold slash (it washed the gold out).
            BladeBlur(s.Fighter, cut, 0.5f);
            BladeGlint(s.Fighter, cut * 0.8f, 0.8f);
            KingSlash(s.Fighter, cut);
            // 10-26: the cut tears the air along its arc (bent air thrown off the tip the way it is going, about every
            // 0.03 s) and a gold light rides with it; the lens shudders as it comes round.
            var f = s.Fighter;
            Vector3 lastTip = f != null && f.SwordBody != null ? f.BladeTip : king.Hips.position;
            float nextAir = 0f;
            kit.Run(cut + 2f * F, (fx, d) =>
            {
                if (f == null || f.SwordBody == null) return false;
                Vector3 tip = f.BladeTip, v = d > 1e-5f ? (tip - lastTip) / d : Vector3.zero;
                lastTip = tip;
                if (fx.age >= nextAir && v.sqrMagnitude > 4f)
                {
                    nextAir = fx.age + 0.03f;
                    Air.Kick(tip, v, Mathf.Clamp(v.magnitude / 6f, 0.8f, 1.5f), null, 0.08f, 1.8f, 1.6f);
                }
                return fx.age < cut;
            });
            SparkLight(king.bodies[(int)BodyId.Chest].position + Kit.FlatDir(king.Facing, Vector3.forward) * 0.6f, 2.4f, cut, 3f, KingGold);
            LensShock(king, null, 0.55f, 0.16f);
            // (No bent air here: on the board's dark it read as grey smoke.) 10-26: no stop at the top of the swing either -
            // the block before it holds, and the hit as the blade crosses his front has its own (a second stop read as a stutter).
            if (!SwordFightSkills.BodyMotion) HitStop(3, king);
            Shake(king, null, 0.12f, 9);
            Punch(king, null, 4f, 14);
        }
    }
}
