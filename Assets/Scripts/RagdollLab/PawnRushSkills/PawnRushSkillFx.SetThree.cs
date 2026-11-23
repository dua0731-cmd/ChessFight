using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The set 3판's solid things and moments on every screen (RagdollPawn.SetThree.cs; JY-skill-lab 11-23), drawn from
    /// the S3 moments and the pieces' plan state, plainly - the user is to try the skills, the look comes later:
    ///
    ///   the bolas    two weights whirling along its flight; wrapped round the legs it catches; stars round a stunned head
    ///   the knight   a streak down its dive (the stomp is the bent leap's own effect)
    ///   the rook     the rolling column (its own piece baked, laid on its side, turning with the way it rolls; the skin
    ///                hidden meanwhile); dust running ahead of the split ground; the tower (the rook baked in stone round
    ///                the one shut in, its top under that one's head), crumbling
    ///   the queen    a flash as the star comes and along each blade that throws; a red flash at each check
    ///   the bishop   a holy sword beside each enemy with its diagonal of light on the floor, then flying; gold rings round
    ///                a piece it binds; the stone rolling through the air, breaking; the bottle's arc, the puddle, a splash
    /// </summary>
    public partial class PawnRushSkillFx
    {
        sealed class S3Bola { public RagdollPawn by; public Vector3 pos, dir; public float speed, flown, range, spin; public bool done; }
        sealed class S3Ring { public RagdollPawn on; public float until, born; public int kind; }   // 0 stars, 1 gold rings, 2 a wrap
        sealed class S3Sword { public int id; public RagdollPawn by, foe; public Vector3 at, dir, to; public float born, fuse, flyAt = -1f, speed, endAt = -1f; }
        sealed class S3Stone { public RagdollPawn by; public int id; public Vector3 pos, vel; public float flown, spin; public bool done; }
        sealed class S3Pool { public Vector3 at; public float radius, until, born; }
        sealed class S3Bottle { public Vector3 from, to; public float born, time; }
        sealed class S3Crack { public RagdollPawn by; public Vector3 from, dir, tip; public float speed, born, nextDust; public bool done; }
        sealed class S3Look
        {
            public RagdollPawn by, inside;
            public Mesh baked;
            public Matrix4x4 canon = Matrix4x4.identity;
            public Vector3 size = Vector3.one;
            public Material[] mats;
            public float angle, born, until;
            public Vector3 feet;
        }

        readonly List<S3Bola> s3Bolas = new List<S3Bola>();
        readonly List<S3Ring> s3Rings = new List<S3Ring>();
        readonly List<S3Sword> s3Swords = new List<S3Sword>();
        readonly List<S3Stone> s3Stones = new List<S3Stone>();
        readonly List<S3Pool> s3Pools = new List<S3Pool>();
        readonly List<S3Bottle> s3Bottles = new List<S3Bottle>();
        readonly List<S3Crack> s3Cracks = new List<S3Crack>();
        readonly Dictionary<RagdollPawn, S3Look> s3Rolls = new Dictionary<RagdollPawn, S3Look>();
        readonly List<RagdollPawn> s3RollsGone = new List<RagdollPawn>();
        readonly List<S3Look> s3Towers = new List<S3Look>();
        Material matS3Glow, matS3Disc;
        bool s3Built;
        // This frame's colour blocks for the shapes drawn straight (no objects made a frame); made on first use - Unity
        // allows no MaterialPropertyBlock in a component's field initializers.
        readonly List<MaterialPropertyBlock> s3Blocks = new List<MaterialPropertyBlock>();
        int s3BlocksUsed;

        static readonly Color S3Gold = new Color(1f, 0.82f, 0.32f);
        static readonly Color S3Holy = new Color(1f, 0.95f, 0.75f);
        static readonly Color S3Water = new Color(0.35f, 0.7f, 1f);
        static readonly Color S3Dust = new Color(0.62f, 0.58f, 0.52f);

        readonly HashSet<string> fxFaults = new HashSet<string>();

        /// <summary>An effect threw: written to the log once (by where and what), and the frame or the moment goes on
        /// without it.</summary>
        void FxFault(string where, System.Exception ex)
        {
            if (fxFaults.Add(where + ":" + ex.GetType().Name + ":" + ex.Message)) Debug.LogException(ex);
        }

        void S3Build()
        {
            if (s3Built) return;
            s3Built = true;
            BuildUltParts();
            BrdBuild();
            BuildPrevizParts();
            UltAudioBuild();
            matS3Glow = Glow(Texture2D.whiteTexture, rim: 0.6f, rimPower: 2f);
            matS3Disc = Glow(texSoft);
        }

        float S3Heard(Vector3 at)
        {
            var me = Viewer;
            return me != null && (me.Hips.position - at).sqrMagnitude < 9f ? 1f : UltHeard(at);
        }

        // ---------------------------------------------------------------- the moments

        void SetThreeFx(SkillFxEvent e)
        {
            S3Build();
            float lvl = S3Heard(e.at);
            switch (e.kind)
            {
                case SkillFxKind.S3Bolas:
                    if (e.by == null) break;
                    s3Bolas.RemoveAll(b => b.by == e.by);
                    s3Bolas.Add(new S3Bola { by = e.by, pos = e.at, dir = e.dir.sqrMagnitude > 1e-4f ? e.dir.normalized : Vector3.forward, speed = e.size, range = Params != null ? Params.bolasRange : 12f });
                    UltClip(UltClipId.Whoosh, 0.5f * lvl, 1.4f);
                    break;
                case SkillFxKind.S3BolasCatch:
                    foreach (var b in s3Bolas) if (b.by == e.by) b.done = true;
                    if (e.target != null) s3Rings.Add(new S3Ring { on = e.target, kind = 2, born = Clock, until = Clock + (Params != null ? Params.bolasPull : 1f) + 0.6f });
                    SparkBurst(e.at, 10, S3Gold, Vector3.up, 80f, 2f, 5f, 0.35f, 0.03f);
                    UltClip(UltClipId.Tap, 0.7f * lvl, 0.8f);
                    break;
                case SkillFxKind.S3BolasEnd:
                    foreach (var b in s3Bolas) if (b.by == e.by) b.done = true;
                    s3Rings.RemoveAll(r => r.kind == 2 && r.on == e.target);
                    if (e.count == 0) DustRing(Ground(e.at), 6, 0.3f, 2f, S3Dust, 0.4f, 0.6f);
                    else if (e.count == 2)
                    {
                        Flare(e.at, Color.white, 1.2f, 5f, 0.18f);
                        SparkBurst(e.at, 14, Color.white, Vector3.up, 90f, 3f, 7f, 0.35f, 0.03f);
                        UltClip(UltClipId.Capture, 0.8f * lvl, 1.2f);
                    }
                    else if (e.count == 3)
                    {
                        DustRing(Ground(e.at), 8, 0.4f, 3f, S3Dust, 0.5f, 0.6f);
                        UltClip(UltClipId.Wah, 0.6f * lvl);
                    }
                    break;
                case SkillFxKind.S3Stun:
                    if (e.target != null) s3Rings.Add(new S3Ring { on = e.target, kind = 0, born = Clock, until = Clock + Mathf.Max(0.3f, e.size) });
                    break;
                case SkillFxKind.S3Bound:
                    if (e.target != null) s3Rings.Add(new S3Ring { on = e.target, kind = 1, born = Clock, until = Clock + Mathf.Max(0.3f, e.size) });
                    UltClip(UltClipId.Shimmer, 0.5f * lvl, 1.3f);
                    break;
                case SkillFxKind.S3HopDive:
                    if (e.by == null) break;
                    {
                        var knight = e.by;
                        float until = Clock + 0.45f;
                        anims.Add(new Streak(root, matTrail, HdrA(SkillMarks.Orange, 2f), 0.35f, 0.4f, () => knight != null && Clock < until ? knight.Hips.position : (Vector3?)null));
                    }
                    UltClip(UltClipId.Whoosh, 0.7f * lvl, 0.8f);
                    break;
                case SkillFxKind.S3Roll:
                    if (e.by != null) S3BakeRoll(e.by);
                    DustRing(Ground(e.at), 10, 0.8f, 4f, S3Dust, 0.8f, 0.8f);
                    UltClip(UltClipId.Castle, 0.6f * lvl, 0.8f);
                    break;
                case SkillFxKind.S3RollEnd:
                    if (e.count == 2)
                    {
                        DustRing(Ground(e.at), 10, 0.6f, 4f, S3Dust, 0.7f, 0.7f);
                        UltClip(UltClipId.Crack, 0.7f * lvl, 0.7f);
                    }
                    else if (e.count == 1) UltClip(UltClipId.Capture, 0.8f * lvl, 0.6f);
                    break;
                case SkillFxKind.S3Crack:
                    if (e.count == 0)
                    {
                        s3Cracks.Add(new S3Crack { by = e.by, from = e.at, tip = e.at, dir = FlatDir(e.dir), speed = e.size, born = Clock });
                        UltClip(UltClipId.Crack, 0.7f * lvl, 0.6f);
                    }
                    else
                    {
                        foreach (var c in s3Cracks) if (!c.done && (c.by == e.by || e.by == null)) { c.done = true; c.tip = e.at; }
                        DustRing(Ground(e.at), 10, 0.5f, 3f, S3Dust, 0.6f, 0.7f);
                    }
                    break;
                case SkillFxKind.S3Tower:
                    if (e.target != null) S3BakeTower(e.by, e.target, e.size);
                    DustRing(Ground(e.at), 12, 0.8f, 4f, S3Dust, 0.8f, 0.9f);
                    UltClip(UltClipId.Castle, 0.9f * lvl, 0.6f);
                    break;
                case SkillFxKind.S3TowerFall:
                    foreach (var t in s3Towers) if (t.inside == e.target) t.until = Mathf.Min(t.until, Clock);
                    DustRing(Ground(e.at), 16, 1f, 5f, S3Dust, 1f, 1f);
                    SparkBurst(e.at + Vector3.up * 0.8f, 18, new Color(0.55f, 0.55f, 0.58f), Vector3.up, 80f, 2f, 6f, 0.8f, 0.06f);
                    UltClip(UltClipId.Crack, 0.8f * lvl, 0.5f);
                    break;
                case SkillFxKind.S3Star:
                    if (e.count == 1)
                    {
                        // The bishop's sign of the swords' summons.
                        Flare(e.at + Vector3.up * 0.8f, S3Holy, 1.5f, 4f, 0.3f);
                        UltClip(UltClipId.Shimmer, 0.7f * lvl, 1f);
                        break;
                    }
                    DustRing(Ground(e.at), 8, 0.5f, 3f, Teal, 0.5f, 0.5f);
                    UltClip(UltClipId.Riser, 0.5f * lvl, 1.6f);
                    break;
                case SkillFxKind.S3StarBlade:
                {
                    if (e.count == 255)
                    {
                        Flare(e.at + Vector3.up * 0.1f, Teal, 1.5f, 3f, 0.2f);
                        UltClip(UltClipId.Tick, 0.5f * lvl);
                        break;
                    }
                    float radius = Params != null ? Params.starRadius : 6f;
                    Vector3 from = e.at + Vector3.up * 0.08f, dir = FlatDir(e.dir);
                    Add(new Shape(root, "S3 star blade", meshCylinder, matBrdLine)
                    {
                        life = 0.35f, at = from, rotation = Quaternion.FromToRotation(Vector3.up, dir), scale = new Vector3(0.12f, radius, 0.12f),
                        color = HdrA(S3Gold, 3f),
                        animate = (s, t) => s.bright = 1f - t,
                    });
                    UltClip(UltClipId.Capture, 0.6f * lvl, 1.1f);
                    break;
                }
                case SkillFxKind.S3Check:
                    Flare(e.at, Blood, 1.6f, 5f, 0.25f);
                    SparkBurst(e.at, 14, Blood, Vector3.up, 80f, 3f, 7f, 0.4f, 0.03f);
                    UltClip(UltClipId.Check, 0.8f * lvl, 1f + 0.08f * e.count);
                    break;
                case SkillFxKind.S3SwordRise:
                    s3Swords.Add(new S3Sword { id = e.count, by = e.by, foe = e.target, at = e.at, dir = e.dir.sqrMagnitude > 1e-4f ? e.dir.normalized : Vector3.forward, born = Clock, fuse = e.size });
                    Flare(e.at, S3Holy, 0.8f, 3f, 0.25f);
                    break;
                case SkillFxKind.S3SwordFly:
                    foreach (var sw in s3Swords)
                        if (sw.by == e.by && sw.id == e.count && sw.flyAt < 0f)
                        {
                            sw.flyAt = Clock;
                            sw.at = e.at;
                            sw.dir = e.dir.sqrMagnitude > 1e-4f ? e.dir.normalized : sw.dir;
                            sw.to = e.normal;
                            sw.speed = Mathf.Max(1f, e.size);
                        }
                    UltClip(UltClipId.Whoosh, 0.4f * lvl, 1.5f);
                    break;
                case SkillFxKind.S3SwordEnd:
                    foreach (var sw in s3Swords) if (sw.by == e.by && sw.id == e.count && sw.endAt < 0f) sw.endAt = Clock;
                    SparkBurst(e.at, e.target != null ? 16 : 8, S3Holy, -e.dir, 70f, 2f, 6f, 0.4f, 0.03f);
                    if (e.target != null) UltClip(UltClipId.Capture, 0.6f * lvl, 1.4f);
                    break;
                case SkillFxKind.S3StoneShot:
                    s3Stones.Add(new S3Stone { by = e.by, id = e.count, pos = e.at, vel = (e.dir.sqrMagnitude > 1e-4f ? e.dir.normalized : Vector3.forward) * e.size });
                    UltClip(UltClipId.Whoosh, 0.6f * lvl, 0.6f);
                    break;
                case SkillFxKind.S3StoneBreak:
                    foreach (var st in s3Stones) if (st.by == e.by && st.id == e.count) st.done = true;
                    DustRing(Ground(e.at), 14, 0.8f, 4f, S3Dust, 0.9f, 0.9f);
                    SparkBurst(e.at, 20, new Color(0.5f, 0.5f, 0.52f), Vector3.up, 90f, 2f, 7f, 0.8f, 0.07f);
                    UltClip(UltClipId.Crack, 0.8f * lvl, 0.8f);
                    break;
                case SkillFxKind.S3WaterThrow:
                    s3Bottles.Add(new S3Bottle { from = e.at, to = e.normal, born = Clock, time = Mathf.Max(0.1f, e.size) });
                    break;
                case SkillFxKind.S3Puddle:
                    s3Pools.Add(new S3Pool { at = e.at, radius = Mathf.Max(0.3f, e.size), born = Clock, until = Clock + (Params != null ? Params.waterSeconds : 6f) });
                    SparkBurst(e.at + Vector3.up * 0.1f, 22, S3Water, Vector3.up, 70f, 2f, 5f, 0.6f, 0.04f);
                    UltClip(UltClipId.Crack, 0.5f * lvl, 1.8f);
                    break;
                case SkillFxKind.S3Slip:
                    SparkBurst(e.at + Vector3.up * 0.1f, 14, S3Water, Vector3.up + FlatDir(e.dir), 60f, 2f, 5f, 0.5f, 0.04f);
                    UltClip(UltClipId.Wah, 0.6f * lvl, 1.1f);
                    break;
            }
        }

        // ---------------------------------------------------------------- every frame (UltStep)

        void SetThreeStepFx(Camera cam)
        {
            if (!s3Built) return;
            s3BlocksUsed = 0;
            float dt = Time.deltaTime;
            S3BolasStep(dt);
            S3RingsStep();
            S3SwordsStep(dt);
            S3StonesStep(dt);
            S3WaterStep();
            S3CracksStep(dt);
            S3RollsStep(dt);
            S3TowersStep();
        }

        /// <summary>A shape for this frame only.</summary>
        /// <summary>A glowing shape this frame only, drawn straight (its own colour block from this frame's pool).</summary>
        void S3Now(Mesh mesh, Material mat, Vector3 at, Quaternion rot, Vector3 scale, Color color, float bright = 1f)
        {
            if (mesh == null || mat == null || bright <= 0.001f) return;
            if (s3BlocksUsed == s3Blocks.Count) s3Blocks.Add(new MaterialPropertyBlock());
            var block = s3Blocks[s3BlocksUsed++];
            block.Clear();
            block.SetColor("_Color", color * bright);
            var rp = new RenderParams(mat)
            {
                layer = 0,
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false,
                matProps = block,
                worldBounds = new Bounds(at, Vector3.one * (scale.magnitude * 2f + 1f)),
            };
            Graphics.RenderMesh(rp, mesh, 0, Matrix4x4.TRS(at, rot, scale));
        }

        void S3Line(Vector3 a, Vector3 b, Color c, float radius)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.02f) return;
            S3Now(meshCylinder, matBrdLine, a, Quaternion.FromToRotation(Vector3.up, d / len), new Vector3(radius, len, radius), c);
        }

        void S3BolasStep(float dt)
        {
            for (int i = s3Bolas.Count - 1; i >= 0; i--)
            {
                var b = s3Bolas[i];
                if (b.done || b.by == null || b.flown >= b.range + 0.5f)
                {
                    s3Bolas.RemoveAt(i);
                    continue;
                }
                float step = b.speed * dt;
                b.pos += b.dir * step;
                b.flown += step;
                b.spin += dt * 1400f;
                // Two weights whirling on their cord round the flight's point.
                Vector3 side = Vector3.Cross(Vector3.up, FlatDir(b.dir));
                Vector3 off = Quaternion.AngleAxis(b.spin, b.dir) * side * 0.22f;
                Vector3 w1 = b.pos + off, w2 = b.pos - off;
                S3Line(w1, w2, HdrA(S3Gold, 1.2f), 0.012f);
                S3Now(meshSphere, matS3Glow, w1, Quaternion.identity, Vector3.one * 0.12f, HdrA(S3Gold, 1.6f));
                S3Now(meshSphere, matS3Glow, w2, Quaternion.identity, Vector3.one * 0.12f, HdrA(S3Gold, 1.6f));
            }
        }

        void S3RingsStep()
        {
            for (int i = s3Rings.Count - 1; i >= 0; i--)
            {
                var r = s3Rings[i];
                if (r.on == null || Clock > r.until)
                {
                    s3Rings.RemoveAt(i);
                    continue;
                }
                float fade = Mathf.Clamp01((r.until - Clock) / 0.25f);
                Vector3 hips = r.on.Hips.position;
                if (r.kind == 0)
                {
                    // Stars round the stunned head.
                    Vector3 head = hips + Vector3.up * 0.45f;
                    for (int k = 0; k < 3; k++)
                    {
                        float a = (Clock * 300f + k * 120f) * Mathf.Deg2Rad;
                        Vector3 p = head + new Vector3(Mathf.Cos(a), 0.05f * Mathf.Sin(a * 2f), Mathf.Sin(a)) * 0.28f;
                        S3Now(meshSphere, matS3Glow, p, Quaternion.identity, Vector3.one * 0.09f, HdrA(S3Gold, 2.4f), fade);
                    }
                }
                else
                {
                    // Gold rings of light (bound), or the bolas' cord wound round the legs.
                    Color c = r.kind == 1 ? HdrA(S3Holy, 2.2f) : HdrA(S3Gold, 1.4f);
                    float[] heights = r.kind == 1 ? new[] { -0.05f, 0.15f } : new[] { -0.2f, -0.12f };
                    foreach (float h in heights)
                    {
                        Vector3 at = hips + Vector3.up * h;
                        float spin = Clock * (r.kind == 1 ? 90f : 400f);
                        S3Now(meshQuad, matRing, at, Quaternion.Euler(90f, spin, 0f), new Vector3(0.55f, 0.55f, 1f), c, fade);
                    }
                }
            }
        }

        void S3SwordsStep(float dt)
        {
            for (int i = s3Swords.Count - 1; i >= 0; i--)
            {
                var sw = s3Swords[i];
                float gone = sw.endAt >= 0f ? Clock - sw.endAt : -1f;
                if (gone > 0.15f || Clock - sw.born > sw.fuse + 4f)
                {
                    s3Swords.RemoveAt(i);
                    continue;
                }
                Vector3 tip;
                if (sw.flyAt < 0f)
                {
                    // Waiting beside its enemy, pointing down its diagonal; the diagonal of light on the floor.
                    float rise = Mathf.Clamp01((Clock - sw.born) / 0.25f);
                    tip = sw.at + Vector3.up * (1f - rise) * 0.6f;
                    Vector3 floor = Ground(sw.at) + Vector3.up * 0.04f;
                    float reach = (Params != null ? Params.swordSide : 5f) * 2f;
                    float warn = Mathf.Clamp01((Clock - sw.born) / Mathf.Max(0.1f, sw.fuse));
                    S3Line(floor, floor + FlatDir(sw.dir) * reach, HdrA(S3Holy, 1f + 1.5f * warn), 0.025f + 0.02f * warn);
                }
                else tip = sw.at + sw.dir * Mathf.Min((Clock - sw.flyAt) * sw.speed, (sw.to - sw.at).magnitude);
                Quaternion turn = Quaternion.LookRotation(sw.dir, Vector3.up);
                float k = gone >= 0f ? 1f - gone / 0.15f : 1f;
                S3Now(meshCube, matS3Glow, tip - sw.dir * 0.35f, turn, new Vector3(0.05f, 0.05f, 0.75f), HdrA(S3Holy, 2.4f), k);
                S3Now(meshCube, matS3Glow, tip - sw.dir * 0.72f, turn, new Vector3(0.3f, 0.05f, 0.05f), HdrA(S3Gold, 2f), k);
            }
        }

        void S3StonesStep(float dt)
        {
            var s = Params;
            float size = s != null ? s.stoneSize : 1.2f, drop = s != null ? s.stoneDrop : 1.5f, range = s != null ? s.stoneRange : 25f;
            for (int i = s3Stones.Count - 1; i >= 0; i--)
            {
                var st = s3Stones[i];
                if (st.done || st.flown > range + 1f)
                {
                    s3Stones.RemoveAt(i);
                    continue;
                }
                st.vel += Vector3.down * drop * dt;
                Vector3 step = st.vel * dt;
                st.pos += step;
                // (On the floor it rolls on, as the host's does.)
                Vector3 probe = st.pos + Vector3.up * size, ground = Ground(probe);
                bool found = (ground - probe).sqrMagnitude > 1e-6f;   // (Ground gives the point back when it finds no floor)
                if (found && st.pos.y - size * 0.5f < ground.y)
                {
                    st.pos.y = ground.y + size * 0.5f;
                    st.vel.y = 0f;
                }
                st.flown += step.magnitude;
                st.spin += step.magnitude / Mathf.Max(0.2f, size * 0.5f) * Mathf.Rad2Deg;
                if (matStone == null) continue;
                Vector3 axis = Vector3.Cross(Vector3.up, FlatDir(st.vel));
                var rp = new RenderParams(matStone) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On, receiveShadows = true, layer = 0 };
                Graphics.RenderMesh(rp, meshSphere, 0, Matrix4x4.TRS(st.pos, Quaternion.AngleAxis(st.spin, axis), Vector3.one * size));
            }
        }

        void S3WaterStep()
        {
            for (int i = s3Bottles.Count - 1; i >= 0; i--)
            {
                var b = s3Bottles[i];
                float t = (Clock - b.born) / b.time;
                if (t >= 1f)
                {
                    s3Bottles.RemoveAt(i);
                    continue;
                }
                Vector3 p = Vector3.Lerp(b.from, b.to, t) + Vector3.up * (4f * 1.5f * t * (1f - t));
                S3Now(meshSphere, matS3Glow, p, Quaternion.identity, Vector3.one * 0.16f, HdrA(S3Water, 2f));
            }
            for (int i = s3Pools.Count - 1; i >= 0; i--)
            {
                var pool = s3Pools[i];
                if (Clock > pool.until)
                {
                    s3Pools.RemoveAt(i);
                    continue;
                }
                float grow = Mathf.Clamp01((Clock - pool.born) / 0.2f), fade = Mathf.Clamp01((pool.until - Clock) / 0.5f);
                float shimmer = 0.85f + 0.15f * Mathf.Sin(Clock * 4f);
                Vector3 at = pool.at + Vector3.up * 0.03f;
                S3Now(meshQuad, matS3Disc, at, Quaternion.Euler(90f, 0f, 0f), new Vector3(pool.radius * 2.3f * grow, pool.radius * 2.3f * grow, 1f), HdrA(S3Water, 0.9f), fade * shimmer);
                S3Now(meshQuad, matRing, at + Vector3.up * 0.01f, Quaternion.Euler(90f, Clock * 20f, 0f), new Vector3(pool.radius * 2f * grow, pool.radius * 2f * grow, 1f), HdrA(S3Water, 1.6f), fade);
            }
        }

        void S3CracksStep(float dt)
        {
            for (int i = s3Cracks.Count - 1; i >= 0; i--)
            {
                var c = s3Cracks[i];
                if (Clock - c.born > 2f)
                {
                    s3Cracks.RemoveAt(i);
                    continue;
                }
                if (c.done) continue;
                c.tip = c.from + c.dir * ((Clock - c.born) * c.speed);
                if (Clock < c.nextDust) continue;
                c.nextDust = Clock + 0.04f;
                DustRing(Ground(c.tip), 3, 0.25f, 2.5f, S3Dust, 0.45f, 0.5f);
            }
        }

        // ---------------------------------------------------------------- the rook's column and tower (its own piece baked)

        /// <summary>The piece's skin baked as it stands, centred and turned back by its facing (as the driven roll's).</summary>
        static bool S3Bake(RagdollPawn p, out Mesh baked, out Matrix4x4 canon, out Vector3 size, out Material[] mats)
        {
            baked = null;
            canon = Matrix4x4.identity;
            size = Vector3.one;
            mats = null;
            var skin = p != null ? p.skin : null;
            if (skin == null) return false;
            baked = new Mesh { name = "S3 piece", hideFlags = HideFlags.HideAndDontSave };
            skin.BakeMesh(baked, true);
            baked.RecalculateBounds();
            Bounds b = baked.bounds;
            Quaternion skinRot = skin.transform.rotation;
            Vector3 skinPos = skin.transform.position, worldCenter = skinPos + skinRot * b.center;
            Vector3 face = new Vector3(p.Facing.x, 0f, p.Facing.z);
            Quaternion facing = Quaternion.LookRotation(face.sqrMagnitude > 1e-4f ? face.normalized : Vector3.forward, Vector3.up);
            canon = Matrix4x4.Rotate(Quaternion.Inverse(facing)) * Matrix4x4.Translate(skinPos - worldCenter) * Matrix4x4.Rotate(skinRot);
            Vector3 lo = Vector3.one * float.MaxValue, hi = Vector3.one * float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(i % 2 == 0 ? b.min.x : b.max.x, i / 2 % 2 == 0 ? b.min.y : b.max.y, i / 4 == 0 ? b.min.z : b.max.z);
                Vector3 c = canon.MultiplyPoint3x4(corner);
                lo = Vector3.Min(lo, c);
                hi = Vector3.Max(hi, c);
            }
            size = Vector3.Max(hi - lo, Vector3.one * 0.05f);
            mats = skin.sharedMaterials;
            return true;
        }

        void S3BakeRoll(RagdollPawn rook)
        {
            if (s3Rolls.TryGetValue(rook, out var old) && old.baked != null) Destroy(old.baked);
            if (!S3Bake(rook, out var baked, out var canon, out var size, out var mats)) return;
            s3Rolls[rook] = new S3Look { by = rook, baked = baked, canon = canon, size = size, mats = mats, born = Clock };
        }

        void S3RollsStep(float dt)
        {
            var s = Params;
            s3RollsGone.Clear();
            foreach (var kv in s3Rolls)
            {
                var rook = kv.Key;
                var look = kv.Value;
                if (rook == null || !rook.RollingShort || look.baked == null || s == null)
                {
                    s3RollsGone.Add(rook);
                    continue;
                }
                var skin = rook.skin;
                if (skin != null) skin.forceRenderingOff = true;
                Vector3 center = rook.Hips.position, dir = FlatDir(rook.PlanDir);
                float speed = Mathf.Lerp(s.rollSpeed0, s.rollSpeed1, Mathf.Clamp01(rook.PlanTime / Mathf.Max(0.1f, s.rollSeconds)));
                look.angle += speed / Mathf.Max(0.2f, s.rollRadius) * Mathf.Rad2Deg * dt;
                // Laid on its side across the way it rolls (its height along the column), turning about that line.
                Quaternion turn = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.AngleAxis(look.angle, Vector3.right) * Quaternion.Euler(0f, 0f, -90f);
                float width = 2f * s.rollRadius / Mathf.Max(0.1f, Mathf.Max(look.size.x, look.size.z)), length = s.rollLength / Mathf.Max(0.1f, look.size.y);
                Matrix4x4 m = Matrix4x4.TRS(center, turn, Vector3.one) * Matrix4x4.Scale(new Vector3(width, length, width)) * look.canon;
                S3Draw(look, m, new Bounds(center, Vector3.one * (s.rollLength + 2f)), skin);
            }
            foreach (var rook in s3RollsGone)
            {
                if (rook != null && rook.skin != null) rook.skin.forceRenderingOff = false;
                if (s3Rolls.TryGetValue(rook, out var look) && look.baked != null) Destroy(look.baked);
                s3Rolls.Remove(rook);
            }
        }

        void S3BakeTower(RagdollPawn rook, RagdollPawn inside, float seconds)
        {
            // (The tower is the rook itself in stone: its own piece if it is one, the piece shut in otherwise.)
            var model = rook != null && rook.Piece == PieceKind.Rook ? rook : inside;
            if (!S3Bake(model, out var baked, out var canon, out var size, out _)) return;
            s3Towers.Add(new S3Look { by = rook, inside = inside, baked = baked, canon = canon, size = size, born = Clock, until = Clock + Mathf.Max(0.3f, seconds), feet = inside.FeetPoint });
        }

        void S3TowersStep()
        {
            var s = Params;
            for (int i = s3Towers.Count - 1; i >= 0; i--)
            {
                var t = s3Towers[i];
                float gone = Clock - t.until;
                if (t.inside == null || gone > 0.5f || t.baked == null || matStone == null)
                {
                    if (t.baked != null) Destroy(t.baked);
                    s3Towers.RemoveAt(i);
                    continue;
                }
                // Its top just under the head of the one inside; as wide as the tower is; sinking as it crumbles.
                Vector3 feet = new Vector3(t.inside.Hips.position.x, t.feet.y, t.inside.Hips.position.z);
                float tall = Mathf.Max(0.4f, t.inside.Hips.position.y + 0.3f - feet.y);
                float wide = s != null ? s.towerWide : 1.6f;
                float rise = Mathf.Clamp01((Clock - t.born) / 0.15f), sink = Mathf.Clamp01(gone / 0.5f);
                Vector3 scale = new Vector3(wide / t.size.x, tall / t.size.y * rise, wide / t.size.z);
                Vector3 at = feet + Vector3.up * (tall * 0.5f * rise - sink * tall);
                Matrix4x4 m = Matrix4x4.TRS(at, Quaternion.identity, Vector3.one) * Matrix4x4.Scale(scale) * t.canon;
                var rp = new RenderParams(matStone) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On, receiveShadows = true, layer = 0, worldBounds = new Bounds(at, Vector3.one * (tall + wide + 1f)) };
                for (int k = 0; k < t.baked.subMeshCount; k++) Graphics.RenderMesh(rp, t.baked, k, m);
            }
        }

        void S3Draw(S3Look look, Matrix4x4 m, Bounds bounds, SkinnedMeshRenderer skin)
        {
            if (look.mats == null || look.mats.Length == 0) return;
            for (int i = 0; i < look.baked.subMeshCount; i++)
            {
                var mat = look.mats[Mathf.Min(i, look.mats.Length - 1)];
                if (mat == null) continue;
                var rp = new RenderParams(mat)
                {
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    receiveShadows = true,
                    layer = skin != null ? skin.gameObject.layer : 0,
                    renderingLayerMask = skin != null ? skin.renderingLayerMask : 1u,
                    worldBounds = bounds,
                };
                Graphics.RenderMesh(rp, look.baked, i, m);
            }
        }

        void DestroySetThree()
        {
            foreach (var kv in s3Rolls)
            {
                if (kv.Key != null && kv.Key.skin != null) kv.Key.skin.forceRenderingOff = false;
                if (kv.Value.baked != null) Destroy(kv.Value.baked);
            }
            s3Rolls.Clear();
            foreach (var t in s3Towers) if (t.baked != null) Destroy(t.baked);
            s3Towers.Clear();
        }
    }
}
