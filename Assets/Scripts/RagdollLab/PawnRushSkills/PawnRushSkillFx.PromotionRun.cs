using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// 승격 (e8=Q!!) on the screens (JY-skill-lab, 2026-11-22; the rules in RagdollPawn.PromotionRun.cs), and the giant
    /// bodies the driven ultimates share (the pawn's 6 m queen, the king's walk).
    ///
    ///   the run     a lane of light on the floor ahead of the pawn (red for the other team: it is coming; teal for the
    ///               pawn's side), sparks and dust off its feet, the comet trail behind
    ///   the queen   a pillar of light and "e8=Q!!" as it leaps; the queen grows to 6 m in the air and comes down with a
    ///               shock wave; its dashes: a line on the floor for 0.3 s, then a streak of light; each enemy hit red,
    ///               the queen 10% bigger
    ///   giants      a piece drawn bigger than its body: its skin baked each frame and drawn scaled about its feet (the
    ///               physics stays the piece's own size); the skin itself hidden meanwhile. On every PC, from the plan.
    /// </summary>
    public partial class PawnRushSkillFx
    {
        sealed class PrFx
        {
            public int dashHits;
            public float leapAt = -1f, warnAt = -10f;
            public Vector3 warnFrom, warnTo;
            public float warnWidth;
        }

        static int PrStageOf(RagdollPawn p) => p != null ? (p.PlanNote >> RagdollPawn.PrStageShift) & 3 : 0;

        void PrCast(UltShow u)
        {
            var f = new PrFx();
            u.state = f;
            // The run's lane of light, ahead of the pawn.
            bool mine = Viewer == u.by;
            Add(new Shape(root, "Pr lane", meshQuad, matTrail)
            {
                life = 1e9f,
                animate = (s, t) =>
                {
                    if (!u.Playing || u.by == null) { s.life = 0f; return; }
                    bool run = PrStageOf(u.by) == RagdollPawn.PrRun;
                    s.bright = run ? 0.8f + 0.2f * Mathf.Sin(Clock * 18f) : 0f;
                    if (!run) return;
                    Vector3 from = FloorUnder(u.by), to = u.by.PlanPoint, d = Flat(to - from);
                    float len = d.magnitude;
                    if (len < 1f) { s.bright = 0f; return; }
                    s.at = from + d * 0.5f + Vector3.up * 0.06f;
                    s.rotation = Quaternion.LookRotation(Vector3.down, d / len);
                    s.scale = new Vector3(3.2f, len, 1f);
                    bool foe = Viewer != null && !mine && Teams.AreEnemies(Viewer.Team, u.by.Team);
                    s.color = foe ? HdrA(Blood, 2f) : HdrA(Teal, 1.6f);
                },
            });
            // Sparks and dust off its feet while it runs.
            anims.Add(new Ongoing((age, dt) =>
            {
                if (!u.Playing || u.by == null) return false;
                if (dt <= 0f || PrStageOf(u.by) != RagdollPawn.PrRun) return true;
                Vector3 feet = FloorUnder(u.by), back = -u.by.PlanDir;
                sparks.Emit(feet + Random.insideUnitSphere * 0.3f, back * Random.Range(3f, 8f) + Vector3.up * Random.Range(1f, 3f), 0.05f, 0.35f, Tint(Random.value < 0.5f ? Color.white : TealPale, 0.2f));
                if (Random.value < 0.5f) smoke.Emit(feet, back * 2f + Vector3.up, Random.Range(0.4f, 0.7f), 0.5f, Tint(new Color(0.6f, 0.6f, 0.62f), 0.1f));
                return true;
            }));
        }

        void PrStep(UltShow u, float dt, Camera cam)
        {
            if (!(u.state is PrFx f) || u.by == null) return;
            var s = Params;
            int stage = PrStageOf(u.by);
            if (!u.Playing || stage < RagdollPawn.PrLeap || s == null) return;
            float giant = PrGiantScale(u.by, s) * Mathf.Min(s.prGrowMax, 1f + s.prGrow * f.dashHits);
            if (stage == RagdollPawn.PrLeap)
            {
                if (f.leapAt < 0f) f.leapAt = Clock;
                giant = Mathf.Lerp(1f, giant, Smooth01((Clock - f.leapAt) / Mathf.Max(0.1f, s.prLeap)));
            }
            WantGiant(u.by, giant, 1f / 0.25f);
        }

        /// <summary>The queen's 6 m against this piece's drawn height.</summary>
        float PrGiantScale(RagdollPawn p, PawnRushSkillParams s)
        {
            float h = GiantBaseHeight(p);
            return h > 0.3f ? s.prTall / h : 4f;
        }

        void PrStateFx(UltShow u, SkillFxEvent e)
        {
            if (!(u.state is PrFx f)) return;
            if (e.count != RagdollPawn.PrLeap) return;
            f.leapAt = Clock;
            Vector3 floor = Ground(e.at);
            // The last rank: a pillar of light, the move written, the queen to come.
            Add(new Shape(root, "Pr promotion pillar", meshCylinder, matPillar)
            {
                life = 1.4f, at = floor,
                animate = (s, t) =>
                {
                    float w = Mathf.Lerp(0.6f, 2.4f, Smooth01(s.Age / 0.3f));
                    s.scale = new Vector3(w, 60f, w);
                    s.color = HdrA(TealPale, 2.6f);
                    s.bright = 1f - Mathf.Clamp01((t - 0.35f) / 0.65f);
                },
            });
            Flare(floor + Vector3.up * 2f, Color.white, 5f, 14f, 0.4f);
            UltSquareWave(floor, FamSquare * 0.5f, 12f, 0.7f, Teal, 3f);
            UltPopWord(floor + Vector3.up * 5f, "e8=Q!!", HdrA(GradeColors[0], 1.5f), 1.3f, 1.6f);
            UltSound(UltSfx.Boom, floor);
            UltPlay(sfxGrade[0], 0.8f, 0.1f);
        }

        void PrLandFx(UltShow u, SkillFxEvent e)
        {
            Vector3 floor = Ground(e.at);
            float radius = Mathf.Max(4f, e.size);
            Flare(floor + Vector3.up * 3f, Color.white, 6f, radius * 2.5f, 0.45f);
            UltSquareWave(floor, FamSquare * 0.5f, radius * 1.4f, 0.9f, Teal, 3f);
            UltSquareWave(floor, FamSquare * 0.5f, radius * 2f, 1.1f, Color.white, 1.4f, 0.08f);
            DustRing(floor, 22, 1.6f, radius * 0.8f, new Color(0.5f, 0.5f, 0.52f), 1.1f, 1.2f);
            SparkBurst(floor + Vector3.up * 0.5f, 70, Ember, Vector3.up, 60f, 6f, 16f, 0.9f, 0.07f);
            UltSound(UltSfx.Impact, floor, e.count > 0 ? RagdollPawn.GradeGreat : RagdollPawn.GradeBest);
            var cam = ViewCamera;
            float near = cam != null ? Mathf.Clamp01(1f - Vector3.Distance(cam.transform.position, floor) / 40f) : 0.5f;
            Shake(Viewer == u.by ? 0.3f : 0.25f * near, 0.7f);
            HitStop(0.1f);
        }

        /// <summary>A dash's line on the floor for 0.3 s before it goes (red for whoever it is coming at).</summary>
        void PrWarnFx(UltShow u, SkillFxEvent e)
        {
            if (!(u.state is PrFx f)) return;
            f.warnAt = Clock;
            f.warnFrom = e.at;
            f.warnTo = e.normal;
            float warn = Params != null ? Params.prWarn : 0.3f, width = (Params != null ? Params.prRadius * Mathf.Min(Params.prGrowMax, 1f + Params.prGrow * f.dashHits) : 2.2f) * 2f;
            f.warnWidth = width;
            Vector3 a = Ground(e.at), b = Ground(e.normal), d = Flat(b - a);
            float len = d.magnitude;
            if (len < 0.5f) return;
            bool mine = Viewer == u.by;
            Add(new Shape(root, "Pr dash line", meshQuad, matTile)
            {
                life = warn + 0.1f, at = a + d * 0.5f + Vector3.up * 0.06f, rotation = Quaternion.LookRotation(Vector3.down, d / len), scale = new Vector3(width, len, 1f), paint = 0.35f,
                animate = (s, t) =>
                {
                    s.color = mine ? HdrA(Teal, 1.6f) : HdrA(Blood, 1.8f);
                    s.bright = Pop(s.Age, 0.05f) * (0.7f + 0.3f * Mathf.Sin(Clock * 30f));
                },
            });
            UltPlay(sfxOff, 0.5f, 0f, 1.6f);
        }

        /// <summary>The dash: a streak of light along it, sparks off the queen.</summary>
        void PrLineFx(UltShow u, SkillFxEvent e)
        {
            Vector3 a = e.at + Vector3.up * 1.5f, b = e.normal + Vector3.up * 1.5f, ab = b - a;
            float len = ab.magnitude, width = Mathf.Max(1f, e.size);
            if (len < 0.5f) return;
            Add(new Shape(root, "Pr dash streak", meshCylinder, matPillar)
            {
                life = 0.7f, at = a, rotation = Quaternion.FromToRotation(Vector3.up, ab / len),
                animate = (s, t) =>
                {
                    float k = Smooth01(s.Age / 0.35f), fade = 1f - Mathf.Clamp01((s.Age - 0.3f) / 0.4f);
                    s.scale = new Vector3(width * 0.5f * fade + 0.02f, len * k, width * 0.5f * fade + 0.02f);
                    s.color = HdrA(TealPale, 2.4f);
                    s.bright = fade;
                },
            });
            Flare(a, Color.white, 3f, 8f, 0.25f);
            SparkBurst(a, 30, TealPale, ab / len, 30f, 6f, 14f, 0.5f, 0.05f);
            UltSound(UltSfx.Boom, a);
            if (Viewer == u.by) Shake(0.14f, 0.3f);
        }

        void PrStrikeFx(UltShow u, SkillFxEvent e)
        {
            if (e.target == null || !(u.state is PrFx f)) return;
            Vector3 at = ChestOf(e.target);
            Flare(at, Blood, 3f, 7f, 0.35f);
            SparkBurst(at, 28, Blood, e.dir, 60f, 4f, 11f, 0.6f, 0.05f);
            if (e.size > 0.5f)
            {
                f.dashHits++;
                UltPopWord(HeadOf(e.target) + Vector3.up * 1.2f, "+10%", HdrA(Teal, 1.4f), 0.5f, 1f);
            }
            UltSound(UltSfx.Stomp, at, e.count);
            UltScreenStrike(u, e);
        }

        string PrHint(UltShow u)
        {
            int stage = PrStageOf(u.by);
            if (stage == RagdollPawn.PrRun) return "이동 키 = 질주 방향 (아무도 못 막아요)";
            if (stage == RagdollPawn.PrLeap) return "퀸으로!";
            byte n = u.by.PlanNote;
            if ((n & (RagdollPawn.PrWindup | RagdollPawn.PrDashing)) != 0) return "돌진!";
            return (n & RagdollPawn.PrDashMask) > 0 ? "좌클릭 = 카메라 쪽으로 30 m 돌진 (낮은 지붕 밑은 못 들어가요)" : "돌진을 다 썼어요";
        }

        string PrLine(UltShow u)
        {
            var f = u.state as PrFx;
            int stage = PrStageOf(u.by), hits = f != null ? f.dashHits : 0;
            if (stage == RagdollPawn.PrRun) return "질주!";
            float grow = Params != null ? Mathf.Min(Params.prGrowMax, 1f + Params.prGrow * hits) : 1f;
            return $"돌진 {u.by.PlanNote & RagdollPawn.PrDashMask}번  ·  크기 ×{grow:0.0}  ·  맞힌 적 {u.struckSet.Count}";
        }

        string PrWarning(UltShow u, RagdollPawn me)
        {
            if (u.by == null || me == null) return null;
            int stage = PrStageOf(u.by);
            Vector3 p = me.Hips.position;
            if (stage == RagdollPawn.PrRun)
                return DistanceToSegment(Flat(p), Flat(u.by.Hips.position), Flat(u.by.PlanPoint)) < 2.5f ? "폰이 질주해 와요 — 옆으로 비켜요!" : null;
            if (u.state is PrFx f && Clock - f.warnAt < 0.45f)
                return DistanceToSegment(Flat(p), Flat(f.warnFrom), Flat(f.warnTo)) < f.warnWidth * 0.5f + 0.5f ? "퀸의 돌진 줄 — 비켜요!" : null;
            return null;
        }

        // ---------------------------------------------------------------- giants

        sealed class Giant
        {
            public RagdollPawn p;
            public float scale = 1f, want = 1f, rate = 4f, height = -1f;
            public Mesh baked;
        }

        readonly Dictionary<RagdollPawn, Giant> giants = new Dictionary<RagdollPawn, Giant>();
        readonly List<RagdollPawn> giantsGone = new List<RagdollPawn>();

        /// <summary>This frame <paramref name="p"/> is drawn <paramref name="scale"/> times its size (eased at
        /// <paramref name="rate"/> a second); not asked again, it shrinks back.</summary>
        void WantGiant(RagdollPawn p, float scale, float rate = 4f)
        {
            if (p == null || p.skin == null) return;
            if (!giants.TryGetValue(p, out var g)) giants[p] = g = new Giant { p = p };
            g.want = Mathf.Max(1f, scale);
            g.rate = rate;
        }

        /// <summary>A piece's drawn height (m) when it first grows (its skin standing).</summary>
        float GiantBaseHeight(RagdollPawn p)
        {
            if (p == null || p.skin == null) return -1f;
            if (giants.TryGetValue(p, out var g) && g.height > 0f) return g.height;
            float h = p.skin.bounds.size.y;
            if (g != null && h > 0.3f) g.height = h;
            return h;
        }

        /// <summary>Every frame (UltStep): the giants drawn, their scale eased; what was not asked for again shrinks back
        /// to its own size and its skin shows again.</summary>
        void UltGiants(float dt)
        {
            giantsGone.Clear();
            foreach (var kv in giants)
            {
                var g = kv.Value;
                var skin = g.p != null ? g.p.skin : null;
                if (skin == null)
                {
                    if (g.baked != null) Destroy(g.baked);
                    giantsGone.Add(kv.Key);
                    continue;
                }
                float step = Mathf.Max(0.5f, g.want) * g.rate * dt;
                g.scale = Mathf.MoveTowards(g.scale, g.want, step);
                g.want = 1f;   // (asked again by the next frame's step, or it shrinks)
                if (g.scale <= 1.01f)
                {
                    skin.forceRenderingOff = false;
                    if (g.baked != null) Destroy(g.baked);
                    giantsGone.Add(kv.Key);
                    continue;
                }
                if (g.baked == null) g.baked = new Mesh { name = "Giant look", hideFlags = HideFlags.HideAndDontSave };
                skin.BakeMesh(g.baked, true);
                skin.forceRenderingOff = true;
                Vector3 feet = FloorUnder(g.p);
                Matrix4x4 world = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                Matrix4x4 big = Matrix4x4.Translate(feet) * Matrix4x4.Scale(Vector3.one * g.scale) * Matrix4x4.Translate(-feet) * world;
                var mats = skin.sharedMaterials;
                var bounds = new Bounds(feet + Vector3.up * 3f * g.scale, Vector3.one * 8f * g.scale);
                for (int i = 0; i < g.baked.subMeshCount && mats.Length > 0; i++)
                {
                    var m = mats[Mathf.Min(i, mats.Length - 1)];
                    if (m == null) continue;
                    var rp = new RenderParams(m)
                    {
                        shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                        receiveShadows = true,
                        layer = skin.gameObject.layer,
                        worldBounds = bounds,
                    };
                    Graphics.RenderMesh(rp, g.baked, i, big);
                }
            }
            foreach (var p in giantsGone) giants.Remove(p);
        }

        void DestroyGiants()
        {
            foreach (var g in giants.Values)
            {
                if (g.p != null && g.p.skin != null) g.p.skin.forceRenderingOff = false;
                if (g.baked != null) Destroy(g.baked);
            }
            giants.Clear();
        }

        /// <summary>How big a piece is drawn now (1 its own size).</summary>
        float GiantScaleOf(RagdollPawn p) => p != null && giants.TryGetValue(p, out var g) ? g.scale : 1f;
    }
}
