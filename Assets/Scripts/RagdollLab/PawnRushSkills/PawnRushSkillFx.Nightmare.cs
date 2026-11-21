using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// 나이트메어 on the screens (JY-skill-lab, 2026-11-22; the rules in RagdollPawn.Nightmare.cs), after the KNIGHTMARE
    /// short: the knight in white-blue lightning with a red eye, leaping from head to head over a darkened course.
    ///
    ///   the knight's screen   a chess square on the floor where the camera looks (teal; red when fastened on an enemy;
    ///                         grey with no sky over it) and a dotted arc to it; a red ring and a thin red pillar of light
    ///                         over every enemy within the leap's reach (dim beyond it, grey under a roof)
    ///   every screen          the landing's red circle, closing, bright and quick in its last 0.5 s; the stomp: lightning
    ///                         to the floor, the square flashing, the capture written over it (N×f7); a fork: "포크!" and
    ///                         red light from the sky on each; an empty landing: grey dust, "빈 땅"
    /// </summary>
    public partial class PawnRushSkillFx
    {
        static readonly string[] NmNotes = { "N×f7", "N×d6", "N×e5", "N×c7", "N×g6", "N×b5", "N×d4", "N×f3", "N×h6", "N×e3" };

        sealed class NmFx
        {
            public readonly Dictionary<RagdollPawn, Shape> marks = new Dictionary<RagdollPawn, Shape>();
            public readonly Shape[] dots = new Shape[14];
            public Vector3 aim;
            public RagdollPawn aimed;
            public bool blocked, haveAim;
            public float goAt = -10f, air = 1f, forkAt = -10f;
        }

        static bool NmFlyingNow(UltShow u) => u.by != null && (u.by.PlanNote & RagdollPawn.NmFlying) != 0;

        void NmCast(UltShow u)
        {
            var f = new NmFx();
            u.state = f;
            if (Viewer != u.by) return;
            float sq = FamSquare;
            // The aim: a chess square where the camera looks, its frame, and the arc's dots to it.
            Add(new Shape(root, "Nm aim square", meshQuad, matTile)
            {
                life = 1e9f, rotation = FlatOnFloor(), scale = new Vector3(sq, sq, 1f), paint = 0.4f,
                animate = (s, t) =>
                {
                    if (!u.Playing) { s.life = 0f; return; }
                    bool show = f.haveAim && !NmFlyingNow(u);
                    s.bright = show ? 0.85f + 0.2f * Mathf.Sin(Clock * 10f) : 0f;
                    if (!show) return;
                    s.at = f.aim + Vector3.up * 0.05f;
                    s.color = f.blocked ? new Color(0.45f, 0.45f, 0.5f) : f.aimed != null ? HdrA(Blood, 1.6f) : HdrA(Teal, 1.3f);
                },
            });
            Add(new Shape(root, "Nm aim edge", meshQuad, matFrame)
            {
                life = 1e9f, rotation = FlatOnFloor(), scale = new Vector3(sq * 1.08f, sq * 1.08f, 1f),
                animate = (s, t) =>
                {
                    if (!u.Playing) { s.life = 0f; return; }
                    bool show = f.haveAim && !NmFlyingNow(u);
                    s.bright = show ? 1f : 0f;
                    if (!show) return;
                    s.at = f.aim + Vector3.up * 0.06f;
                    s.color = f.blocked ? new Color(0.6f, 0.6f, 0.65f) : f.aimed != null ? HdrA(Blood, 3f) : HdrA(TealPale, 3f);
                },
            });
            for (int i = 0; i < f.dots.Length; i++)
            {
                float k = (i + 1f) / (f.dots.Length + 1f);
                f.dots[i] = Add(new Shape(root, "Nm arc dot", meshQuad, matRing)
                {
                    life = 1e9f, billboard = true,
                    animate = (s, t) =>
                    {
                        if (!u.Playing) { s.life = 0f; return; }
                        bool show = f.haveAim && !f.blocked && !NmFlyingNow(u) && u.by != null;
                        s.bright = show ? 0.55f + 0.45f * Mathf.Clamp01(Mathf.Sin((k * 6f - Clock * 3f) * Mathf.PI)) : 0f;
                        if (!show) return;
                        Vector3 a = u.by.Hips.position, b = f.aim + Vector3.up * 0.9f;
                        float across = Flat(b - a).magnitude, high = Params != null ? Params.nmHigh + across * Params.nmHighPer : 9f;
                        s.at = Vector3.Lerp(a, b, k) + Vector3.up * (4f * high * k * (1f - k));
                        float size = 0.16f + 0.1f * Mathf.Clamp01(across / 60f);
                        s.scale = new Vector3(size, size, 1f);
                        s.color = f.aimed != null ? HdrA(Blood, 2f) : HdrA(TealPale, 2f);
                    },
                });
            }
        }

        void NmStep(UltShow u, float dt, Camera cam)
        {
            if (!(u.state is NmFx f) || u.by == null) return;
            var s = Params;
            if (s == null || Viewer != u.by || !u.Playing) return;
            float reach = RagdollPawn.NmRange(s, u.by.PlanNote & RagdollPawn.NmLevelMask);
            f.haveAim = cam != null;
            if (cam != null && !NmFlyingNow(u))
                f.aim = RagdollPawn.NmAim(u.by, ViewAim(cam), reach, s, u.struckSet, out f.aimed, out f.blocked);
            foreach (var e in RagdollPawn.All)
            {
                if (e == null || e == u.by || !Teams.AreEnemies(e.Team, u.by.Team) || f.marks.ContainsKey(e)) continue;
                f.marks[e] = NmMark(u, f, e);
            }
        }

        /// <summary>The red ring and the thin pillar of light over an enemy, on the knight's screen.</summary>
        Shape NmMark(UltShow u, NmFx f, RagdollPawn e)
        {
            // 0 hidden, 1 in reach, 2 beyond it, 3 under a roof; checked a few times a second.
            int state = 0;
            float next = 0f;
            void Check()
            {
                if (Clock < next) return;
                next = Clock + 0.15f;
                var s = Params;
                if (s == null || u.by == null || e == null || e.State != PawnState.Active || u.struckSet.Contains(e)) { state = 0; return; }
                float reach = RagdollPawn.NmRange(s, u.by.PlanNote & RagdollPawn.NmLevelMask);
                float far = Flat(e.Hips.position - u.by.Hips.position).magnitude;
                state = !RagdollPawn.SkyOver(e.FeetPoint, s.nmSky) ? 3 : far <= reach + s.nmSnap ? 1 : 2;
            }
            Add(new Shape(root, "Nm mark pillar", meshCylinder, matPillar)
            {
                life = 1e9f,
                animate = (s, t) =>
                {
                    if (!u.Playing || e == null) { s.life = 0f; return; }
                    Check();
                    bool aimed = f.aimed == e;
                    s.bright = state == 1 ? (aimed ? 1f : 0.55f) : state == 2 ? 0.18f : 0f;
                    if (s.bright <= 0f) return;
                    Vector3 head = HeadOf(e);
                    s.at = head + Vector3.up * 0.8f;
                    s.scale = new Vector3(aimed ? 0.22f : 0.12f, 9f, aimed ? 0.22f : 0.12f);
                    s.color = HdrA(Blood, aimed ? 3f : 2f);
                },
            });
            return Add(new Shape(root, "Nm mark ring", meshQuad, matRing)
            {
                life = 1e9f, billboard = true,
                animate = (s, t) =>
                {
                    if (!u.Playing || e == null) { s.life = 0f; return; }
                    Check();
                    bool aimed = f.aimed == e;
                    s.bright = state == 0 ? 0f : state == 1 ? (aimed ? 1.2f : 0.85f) + 0.2f * Mathf.Sin(Clock * 9f) : 0.4f;
                    if (state == 0) return;
                    float size = state == 1 ? (aimed ? 0.85f : 0.6f) : 0.4f;
                    s.at = HeadOf(e) + Vector3.up * 0.6f;
                    s.scale = new Vector3(size, size, 1f);
                    s.color = state == 3 ? new Color(0.55f, 0.55f, 0.6f) : HdrA(Blood, state == 1 ? 2.6f : 1.2f);
                },
            });
        }

        /// <summary>Before the key: the enemies the first leap would reach, on the knight's screen.</summary>
        void NmPreview(RagdollPawn me, PawnRushSkillParams s, HashSet<RagdollPawn> wanted)
        {
            foreach (var e in RagdollPawn.All)
            {
                if (e == null || e == me || !Teams.AreEnemies(e.Team, me.Team) || e.State != PawnState.Active || e.SkillUntargetable) continue;
                if (Flat(e.Hips.position - me.Hips.position).magnitude > s.nmRange0 + s.nmSnap) continue;
                if (!RagdollPawn.SkyOver(e.FeetPoint, s.nmSky)) continue;
                wanted.Add(e);
            }
        }

        /// <summary>A leap: the take-off, and the landing's red circle every screen sees, closing on where it comes down.</summary>
        void NmGoFx(UltShow u, SkillFxEvent e)
        {
            if (!(u.state is NmFx f)) return;
            f.goAt = Clock;
            f.air = Mathf.Max(0.3f, e.size);
            Vector3 from = u.by != null ? FloorUnder(u.by) : e.at;
            UltSquareWave(from, FamSquare * 0.5f, 6f, 0.45f, Electric, 2f);
            Flare(from + Vector3.up, Electric, 2.5f, 6f, 0.3f);
            SparkBurst(from + Vector3.up * 0.2f, 22, Electric, Vector3.up, 45f, 4f, 9f, 0.5f, 0.05f);
            UltSound(UltSfx.Leap, from, u.struck);
            float radius = (Params != null ? Params.nmStomp : 3f) * 2f, lockAt = Params != null ? Params.nmLock : 0.5f;
            float goAt = f.goAt, air = f.air;
            Add(new Shape(root, "Nm landing circle", meshQuad, matRing)
            {
                life = air + 0.25f, rotation = FlatOnFloor(),
                animate = (s, t) =>
                {
                    float left = goAt + air - Clock;
                    if (left < -0.05f || u.by == null) { s.bright = 0f; return; }
                    bool locked = left <= lockAt;
                    float grow = locked ? 1f : Mathf.Lerp(1.7f, 1.05f, Mathf.Clamp01(1f - (left - lockAt) / Mathf.Max(0.1f, air - lockAt)));
                    s.at = u.by.PlanPoint + Vector3.up * 0.07f;
                    s.scale = new Vector3(radius * grow, radius * grow, 1f);
                    s.color = HdrA(Blood, locked ? 2.8f : 1.5f);
                    s.bright = locked ? 0.75f + 0.45f * Mathf.Abs(Mathf.Sin(Clock * 18f)) : 0.6f;
                },
            });
            Add(new Shape(root, "Nm landing square", meshQuad, matTile)
            {
                life = air + 0.25f, rotation = FlatOnFloor(), scale = new Vector3(FamSquare, FamSquare, 1f), paint = 0.45f,
                animate = (s, t) =>
                {
                    float left = goAt + air - Clock;
                    if (left < -0.05f || u.by == null) { s.bright = 0f; return; }
                    s.at = u.by.PlanPoint + Vector3.up * 0.05f;
                    s.color = HdrA(Blood, 1.4f);
                    s.bright = Mathf.Clamp01(1f - left / air) * (left <= lockAt ? 1f : 0.5f);
                },
            });
        }

        /// <summary>A head stomped: lightning to the floor, its square flashing, the capture written over it; a fork's red
        /// light from the sky.</summary>
        void NmStrikeFx(UltShow u, SkillFxEvent e)
        {
            if (e.target == null) return;
            var f = u.state as NmFx;
            Vector3 head = HeadOf(e.target), floor = Ground(e.target.Hips.position);
            bool fork = e.size > 0.5f;
            Flare(head + Vector3.up * 0.3f, Electric, 3.8f, 7f, 0.3f);
            SparkBurst(head + Vector3.up * 0.2f, 36, Color.white, Vector3.up, 75f, 3f, 9f, 0.6f, 0.05f);
            if (arc != null)
                for (int i = 0; i < 6; i++)
                    arc.Strike(head, floor + Random.insideUnitSphere.normalized * 1.1f, HdrA(Electric, 1.9f), 0.15f, 0.1f, 1f, 0.35f, 1);
            Add(new Shape(root, "Nm stomp square", meshQuad, matTile)
            {
                life = 1f, at = floor + Vector3.up * 0.05f, rotation = FlatOnFloor(), scale = new Vector3(FamSquare, FamSquare, 1f), paint = 0.45f,
                animate = (s, t) => { s.color = HdrA(t < 0.3f ? Blood : Teal, 1.8f); s.bright = 1f - t; },
            });
            string note = NmNotes[(Mathf.Max(1, e.count) - 1) % NmNotes.Length] + (fork ? "+" : "");
            UltPopWord(head + Vector3.up * 1.1f, note, fork ? HdrA(Blood, 1.5f) : TealPale, 0.6f, 1.2f);
            if (fork)
            {
                UltRedBeam(e.target);
                if (f != null && Clock - f.forkAt > 0.3f)
                {
                    f.forkAt = Clock;
                    UltPopWord(head + Vector3.up * 2.2f, "포크!", HdrA(Blood, 1.8f), 0.9f, 1.4f);
                }
            }
            UltSound(UltSfx.Stomp, head, e.count);
            UltScreenStrike(u, e);
        }

        /// <summary>The landing: a square wave and dust; the next leap's reach over it, or "빈 땅" when nobody was there.</summary>
        void NmLandFx(UltShow u, SkillFxEvent e)
        {
            Vector3 floor = Ground(e.at);
            if (e.count <= 0)
            {
                DustRing(floor, 10, 0.6f, 4f, new Color(0.55f, 0.55f, 0.57f), 0.7f, 0.7f);
                UltPopWord(floor + Vector3.up * 1.6f, $"빈 땅 · {(Params != null ? Params.nmRange0 : 40f):0} m", new Color(0.72f, 0.72f, 0.76f), 0.45f, 1.1f);
                UltSound(UltSfx.Off, floor);
                return;
            }
            int level = Mathf.RoundToInt(e.size);
            UltSquareWave(floor, FamSquare * 0.5f, 12f, 0.6f, Teal, 2.4f);
            DustRing(floor, 14, 1f, 6f, new Color(0.5f, 0.5f, 0.52f), 0.9f, 0.9f);
            Flare(floor + Vector3.up, Electric, 2.8f, 9f, 0.3f);
            float reach = Params != null ? RagdollPawn.NmRange(Params, level) : 60f;
            UltPopWord(floor + Vector3.up * 2.6f, $"다음 {reach:0} m" + (level >= 2 ? "!!" : "!"), HdrA(Teal, 1.3f), 0.5f, 1.2f);
            var cam = ViewCamera;
            float near = cam != null ? Mathf.Clamp01(1f - Vector3.Distance(cam.transform.position, floor) / 30f) : 0.5f;
            Shake(Viewer == u.by ? 0.16f : 0.12f * near, 0.35f);
        }

        string NmHint(UltShow u)
        {
            if (u.by == null) return null;
            byte n = u.by.PlanNote;
            if ((n & RagdollPawn.NmFlying) != 0) return "이동 키 = 착지점 조금 고치기 (빨간 원에서 적이 비키면 놓쳐요)";
            if (u.state is NmFx f && f.blocked) return "하늘이 막힌 곳 — 다른 데를 봐요";
            if ((n & RagdollPawn.NmRiding) != 0) return "머리 위! 좌클릭 = 다음 도약";
            return "좌클릭 = 보는 곳으로 도약 (빨간 표식 가까이면 그 적에게 붙어요)";
        }

        string NmLine(UltShow u)
        {
            float reach = Params != null && u.by != null ? RagdollPawn.NmRange(Params, u.by.PlanNote & RagdollPawn.NmLevelMask) : 40f;
            return $"도약 {reach:0} m  ·  밟은 적 {u.struckSet.Count}";
        }

        string NmWarning(UltShow u, RagdollPawn me)
        {
            if (u.by == null || me == null || !NmFlyingNow(u)) return null;
            float radius = (Params != null ? Params.nmStomp : 3f) + 1f;
            return Flat(me.Hips.position - u.by.PlanPoint).magnitude <= radius ? "나이트가 내려와요 — 빨간 원에서 비켜요!" : null;
        }
    }
}
