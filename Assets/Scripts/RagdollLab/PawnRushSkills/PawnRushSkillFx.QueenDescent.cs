using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// 여왕 강림 on the screens (JY-skill-lab, 2026-11-22; the rules in RagdollPawn.QueenDescent.cs).
    ///
    ///   the queen's screen   where the camera looks on the floor: the dive's circle (8 m, teal), the sacrifice's faint
    ///                        around it (16 m), a square in the middle; grey with no sky over it
    ///   every screen         her shadow growing on the point for the second she comes down (the sacrifice's red and
    ///                        twice as wide, "퀸 희생!!" over it), a falling whistle; the blow: light, square waves, dust;
    ///                        the sacrifice's board on fire and "Q×f7+!!"
    /// </summary>
    public partial class PawnRushSkillFx
    {
        sealed class QdFx
        {
            public Vector3 aim;
            public bool haveAim, blocked;
        }

        Material matShadow;   // (made with the ultimates' parts: a soft disc that covers what is under it)

        static int QdStageOf(RagdollPawn p) => p != null ? (p.PlanNote >> RagdollPawn.QdStageShift) & 3 : 0;

        void QdCast(UltShow u)
        {
            var f = new QdFx();
            u.state = f;
            if (matShadow == null) matShadow = Glow(texSoft, opacity: 1f);
            if (Viewer != u.by) return;
            float r = Params != null ? Params.qdRadius : 8f, sac = Params != null ? Params.qdSacRadius : 16f;
            bool Show() => u.Playing && f.haveAim && QdStageOf(u.by) == RagdollPawn.QdFly && (u.by.PlanNote & RagdollPawn.QdDivesMask) > 0;
            Add(new Shape(root, "Qd aim ring", meshQuad, matRing)
            {
                life = 1e9f, rotation = FlatOnFloor(), scale = new Vector3(r * 2f, r * 2f, 1f),
                animate = (s, t) =>
                {
                    if (!u.Playing) { s.life = 0f; return; }
                    bool show = Show();
                    s.bright = show ? 0.8f + 0.2f * Mathf.Sin(Clock * 8f) : 0f;
                    if (!show) return;
                    s.at = f.aim + Vector3.up * 0.08f;
                    s.color = f.blocked ? new Color(0.5f, 0.5f, 0.55f) : HdrA(Teal, 2.2f);
                },
            });
            Add(new Shape(root, "Qd sacrifice ring", meshQuad, matRing)
            {
                life = 1e9f, rotation = FlatOnFloor(), scale = new Vector3(sac * 2f, sac * 2f, 1f),
                animate = (s, t) =>
                {
                    if (!u.Playing) { s.life = 0f; return; }
                    bool show = Show() && !f.blocked;
                    s.bright = show ? 0.25f : 0f;
                    if (!show) return;
                    s.at = f.aim + Vector3.up * 0.07f;
                    s.color = HdrA(Blood, 1.6f);
                },
            });
            Add(new Shape(root, "Qd aim square", meshQuad, matTile)
            {
                life = 1e9f, rotation = FlatOnFloor(), scale = new Vector3(FamSquare, FamSquare, 1f), paint = 0.4f,
                animate = (s, t) =>
                {
                    if (!u.Playing) { s.life = 0f; return; }
                    bool show = Show();
                    s.bright = show ? 0.9f : 0f;
                    if (!show) return;
                    s.at = f.aim + Vector3.up * 0.06f;
                    s.color = f.blocked ? new Color(0.45f, 0.45f, 0.5f) : HdrA(Teal, 1.4f);
                },
            });
        }

        void QdStep(UltShow u, float dt, Camera cam)
        {
            if (!(u.state is QdFx f) || u.by == null || Viewer != u.by || cam == null || Params == null) return;
            if (QdStageOf(u.by) != RagdollPawn.QdFly) return;
            f.aim = RagdollPawn.DriveAimPoint(u.by, ViewAim(cam), Params.qdReach, out _);
            f.blocked = !RagdollPawn.SkyOver(f.aim, Mathf.Max(4f, u.by.Hips.position.y - f.aim.y - 3f));
            f.haveAim = true;
        }

        /// <summary>The dive: her shadow growing on the point for the second she takes to come down.</summary>
        void QdWarnFx(UltShow u, SkillFxEvent e)
        {
            bool sac = e.count == 2;
            float r = Mathf.Max(2f, e.size), time = Params != null ? Params.qdDiveTime : 1f;
            Vector3 at = Ground(e.at) + Vector3.up * 0.09f;
            Add(new Shape(root, "Qd shadow", meshQuad, matShadow)
            {
                life = time + 0.05f, at = at, rotation = FlatOnFloor(),
                animate = (s, t) =>
                {
                    float k = Smooth01(s.Age / time);
                    float size = r * 2f * Mathf.Lerp(0.35f, 1.05f, k);
                    s.scale = new Vector3(size, size, 1f);
                    s.color = new Color(0.02f, 0.02f, 0.03f, 1f);
                    s.bright = 0.25f + 0.6f * k;
                },
            });
            Add(new Shape(root, "Qd shadow rim", meshQuad, matRing)
            {
                life = time + 0.05f, at = at + Vector3.up * 0.01f, rotation = FlatOnFloor(),
                animate = (s, t) =>
                {
                    float k = Smooth01(s.Age / time);
                    float size = r * 2f * Mathf.Lerp(1.4f, 1f, k);
                    s.scale = new Vector3(size, size, 1f);
                    s.color = HdrA(sac ? Blood : Color.Lerp(Blood, Color.white, 0.3f), sac ? 2.8f : 1.8f);
                    s.bright = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Clock * (sac ? 16f : 10f)));
                },
            });
            if (sac) UltPopWord(at + Vector3.up * 4f, "퀸 희생!!", HdrA(Blood, 1.7f), 1.1f, time + 0.4f);
            UltAudioBuild();
            UltPlay(sfxWhistle, 0.8f, 0f, sac ? 0.85f : 1f);
        }

        /// <summary>The blow: light, square waves and dust; the sacrifice's board on fire.</summary>
        void QdLandFx(UltShow u, SkillFxEvent e)
        {
            Vector3 floor = Ground(e.at);
            float r = Mathf.Max(3f, e.size);
            bool sac = (u.by != null && (u.by.PlanNote & RagdollPawn.QdSacrifice) != 0) || r > 12f;
            Flare(floor + Vector3.up * 2f, Color.white, sac ? 7f : 5f, r * 2.4f, 0.45f);
            UltSquareWave(floor, FamSquare * 0.5f, r * 1.3f, 0.8f, sac ? Blood : Teal, 3f);
            UltSquareWave(floor, FamSquare * 0.5f, r * 1.9f, 1.1f, Color.white, 1.5f, 0.08f);
            DustRing(floor, 24, 1.6f, r * 0.75f, new Color(0.5f, 0.5f, 0.52f), 1.1f, 1.2f);
            SparkBurst(floor + Vector3.up * 0.5f, sac ? 110 : 60, sac ? Ember : Color.white, Vector3.up, 60f, 6f, 16f, 0.9f, 0.07f);
            if (sac)
            {
                UltFireBoard(floor, r * 0.75f);
                UltPopWord(floor + Vector3.up * 6f, "Q×f7+!!", HdrA(GradeColors[0], 1.6f), 1.4f, 1.8f);
                Add(new Shape(root, "Qd sacrifice pillar", meshCylinder, matPillar)
                {
                    life = 1.4f, at = floor,
                    animate = (s, t) =>
                    {
                        float w = Mathf.Lerp(1f, 4f, Smooth01(s.Age / 0.25f));
                        s.scale = new Vector3(w, 70f, w);
                        s.color = HdrA(Blood, 3f);
                        s.bright = 1f - Mathf.Clamp01((t - 0.25f) / 0.75f);
                    },
                });
            }
            UltSound(UltSfx.Impact, floor, sac ? RagdollPawn.GradeBrilliant : e.count > 0 ? RagdollPawn.GradeGreat : RagdollPawn.GradeBest);
            var cam = ViewCamera;
            float near = cam != null ? Mathf.Clamp01(1f - Vector3.Distance(cam.transform.position, floor) / 45f) : 0.5f;
            Shake(Viewer == u.by ? 0.3f : (sac ? 0.32f : 0.22f) * near, sac ? 0.9f : 0.6f);
            HitStop(sac ? 0.16f : 0.08f);
        }

        void QdStrikeFx(UltShow u, SkillFxEvent e)
        {
            if (e.target == null) return;
            Vector3 at = ChestOf(e.target);
            Flare(at, Blood, 3f, 7f, 0.35f);
            SparkBurst(at, 24, Blood, e.dir + Vector3.up, 60f, 4f, 11f, 0.6f, 0.05f);
            UltSound(UltSfx.Stomp, at, e.count);
            UltScreenStrike(u, e);
        }

        void QdStateFx(UltShow u, SkillFxEvent e)
        {
            if (e.count != RagdollPawn.QdDown) return;
            Vector3 floor = Ground(e.at);
            DustRing(floor, 14, 0.8f, 4f, new Color(0.55f, 0.55f, 0.57f), 0.8f, 0.8f);
            UltSquareWave(floor, FamSquare * 0.5f, 6f, 0.5f, Teal, 2f);
        }

        string QdHint(UltShow u)
        {
            if (u.by == null) return null;
            int stage = QdStageOf(u.by), dives = u.by.PlanNote & RagdollPawn.QdDivesMask;
            if (stage == RagdollPawn.QdDive) return (u.by.PlanNote & RagdollPawn.QdSacrifice) != 0 ? "퀸 희생!!" : "내리꽂기!";
            if (stage == RagdollPawn.QdRise) return "솟는 중…";
            if (stage == RagdollPawn.QdDown) return "내려서는 중";
            if (u.state is QdFx f && f.blocked) return "지붕 밑에는 못 내리꽂아요 — 다른 데를 봐요";
            return dives > 0 ? "이동 키 = 날기  ·  좌클릭 = 보는 곳에 내리꽂기  ·  우클릭 = 퀸 희생 (16 m, 퀸은 잡혀요)" : "내리꽂기를 다 썼어요";
        }

        string QdLine(UltShow u) => $"내리꽂기 {(u.by != null ? u.by.PlanNote & RagdollPawn.QdDivesMask : 0)}번  ·  날린 적 {u.struckSet.Count}";

        string QdWarning(UltShow u, RagdollPawn me)
        {
            if (u.by == null || me == null || QdStageOf(u.by) != RagdollPawn.QdDive || Params == null) return null;
            float r = (u.by.PlanNote & RagdollPawn.QdSacrifice) != 0 ? Params.qdSacRadius : Params.qdRadius;
            return Flat(me.Hips.position - u.by.PlanPoint).magnitude <= r + 1f ? "퀸이 내리꽂아요 — 그림자에서 흩어져요!" : null;
        }

        /// <summary>Before the key: the enemies within the dive's reach under open sky, on the queen's screen.</summary>
        void QdPreview(RagdollPawn me, PawnRushSkillParams s, HashSet<RagdollPawn> wanted)
        {
            foreach (var e in RagdollPawn.All)
            {
                if (e == null || e == me || !Teams.AreEnemies(e.Team, me.Team) || e.State != PawnState.Active || e.SkillUntargetable) continue;
                if (Flat(e.Hips.position - me.Hips.position).magnitude > s.qdReach) continue;
                if (!RagdollPawn.SkyOver(e.FeetPoint, 8f)) continue;
                wanted.Add(e);
            }
        }
    }
}
