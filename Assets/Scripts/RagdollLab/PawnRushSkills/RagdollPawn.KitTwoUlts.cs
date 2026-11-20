using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The kits' second version, its new ultimate (JY-skill-lab, 2026-11-21; Docs/Skills/PAWN_RUSH_SKILL_KITS.md 6.4, R-궁).
    /// The other five are the pool's (RagdollPawn.PoolUltimates.cs) with this version's warnings and numbers.
    ///
    ///   R-궁 7번째 줄   the key: a high wall (2.5 m) rises across the whole way (8 m, short of the side walls) in front of
    ///                   the rook, the way it looks - a 1 s warning everyone is told of. For 8 s the wall goes as far as the
    ///                   rook pushes it from behind (20 m at most), never back; the enemies in front of it are pushed on
    ///                   with it (over an edge they fall). Turned back when raised, it pushes the ones following; sideways,
    ///                   the ones by the way. Its own team walks through it. The left click held and let go gathers a
    ///                   blow: the wall jumps up to 3 m at once, throwing the ones in front on.
    ///
    /// The wall is a PawnRushKitWall that moves (a kinematic body on the host; another PC moves its picture from the
    /// rook's skill state). Not seen in Unity by anyone yet.
    /// </summary>
    public partial class RagdollPawn
    {
        PawnRushKitWall rankWall;
        Vector3 rankStart;
        float rankAlong, rankWidthNow, rankChargeT;
        bool rankHeld, rankCharging;

        /// <summary>The 7th rank's wall's two ends on the floor (any PC: its middle is the plan point, its width the small
        /// number in tenths of a metre).</summary>
        public void RankEnds(out Vector3 left, out Vector3 right)
        {
            Vector3 side = Vector3.Cross(Vector3.up, FlatDir(planDir));
            float half = planNote * 0.05f;
            left = planPoint - side * half;
            right = planPoint + side * half;
        }

        /// <summary>The way's width where the wall rises: out to rankWidth / 2 each side, short of a side wall.</summary>
        float RankWidthAt(PawnRushSkillParams s, Vector3 middle, Vector3 fwd)
        {
            Vector3 side = Vector3.Cross(Vector3.up, fwd), from = middle + Vector3.up * 1f;
            float half = s.rankWidth * 0.5f, left = half, right = half;
            if (SolidRay(from, -side, half, out var hl)) left = Mathf.Max(0.5f, hl.distance - 0.1f);
            if (SolidRay(from, side, half, out var hr)) right = Mathf.Max(0.5f, hr.distance - 0.1f);
            // (Kept centred on the line: the narrower side sets both.)
            return 2f * Mathf.Min(left, right);
        }

        void UpdateSeventhRank(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                {
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    Vector3 fwd = AimFlat();
                    planDir = fwd;
                    facing = fwd;
                    rankAlong = 0f;
                    rankStart = FeetPoint + fwd * s.rankGap;
                    rankWidthNow = RankWidthAt(s, rankStart, fwd);
                    planPoint = rankStart;
                    planNote = (byte)Mathf.Clamp(Mathf.RoundToInt(rankWidthNow * 10f), 10, 255);
                    rankChargeT = 0f;
                    rankHeld = rankCharging = false;
                    HaltDash();
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    float stands = s.ultWarn + s.rankSeconds + 0.3f;
                    RankEnds(out var l, out var r);
                    rankWall = PawnRushKitWall.Raise(PawnRushKitWall.NextId(), this, l, r, s.rankHeight, s.rankThick, stands, s.ultWarn, true);
                    rankWall.MakeMoving();
                    foreach (var mate in All)
                        if (mate != null && mate != this && mate.team == team) mate.IgnoreBody(rankWall.Collider, true);   // its team walks through
                    Fx(SkillFxKind.KitRankWall, null, l, fwd, rankWall.Id, stands, r);
                    Fx(SkillFxKind.RookSlam, null, FeetPoint, fwd, 0);   // (the iron gate: the rook's slam, borrowed)
                    SkillDetail = "7번째 줄: 벽이 솟음";
                    Log($"{DisplayName}: 7번째 줄 — 폭 {rankWidthNow:0.#} m 벽, {s.ultWarn:0.#}초 뒤 {s.rankSeconds:0}초 동안 밀기");
                    break;
                }
                case PlanPhase.Windup:
                    planTime += dt;
                    if (planTime < s.ultWarn) break;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    SkillDetail = "7번째 줄: 벽 뒤에서 달리면 밀려요 (좌클릭 모아 치기)";
                    break;
                case PlanPhase.Active:
                {
                    planTime += dt;
                    Vector3 fwd = FlatDir(planDir);
                    float before = rankAlong;
                    // The wall goes as far as the rook pushes it from behind, never back, rankMax at most.
                    float pushedTo = Vector3.Dot(Flat(FeetPoint - rankStart), fwd) + s.rankGap;
                    rankAlong = Mathf.Clamp(Mathf.Max(rankAlong, pushedTo), 0f, s.rankMax);
                    // The left click held gathers a blow; let go, the wall jumps.
                    bool blow = false;
                    if (rankHeld) rankChargeT += dt;
                    else if (rankChargeT > 0f)
                    {
                        float jump = s.rankJump * Mathf.Clamp01(rankChargeT / Mathf.Max(0.05f, s.rankCharge));
                        rankAlong = Mathf.Min(s.rankMax, rankAlong + jump);
                        rankChargeT = 0f;
                        blow = jump > 0.2f;
                    }
                    planPoint = rankStart + fwd * rankAlong;
                    if (rankWall != null)
                    {
                        RankEnds(out var l, out var r);
                        rankWall.SetLine(l, r);
                    }
                    RankPush(s, fwd, before, rankAlong, dt, blow);
                    if (planTime >= s.rankSeconds) EndSeventhRank(s, $"{s.rankSeconds:0}초");
                    break;
                }
                default:
                    EndSeventhRank(s, "끊김");
                    break;
            }
        }

        /// <summary>The enemies just in front of the wall go on with it (faster by rankPushOver); a blow throws the ones its
        /// jump swept on rankJump more.</summary>
        void RankPush(PawnRushSkillParams s, Vector3 fwd, float from, float to, float dt, bool blow)
        {
            float moved = to - from;
            if (moved <= 0.0005f) return;
            Vector3 side = Vector3.Cross(Vector3.up, fwd);
            float half = planNote * 0.05f + 0.3f, face = to + s.rankThick * 0.5f, speed = moved / Mathf.Max(1e-3f, dt);
            int n = 0;
            foreach (var e in All)
            {
                if (e == null || e == this || !IsEnemyOf(e) || e.SkillUntargetable) continue;
                Vector3 d = Flat(e.bodies[0].position - rankStart);
                float along = Vector3.Dot(d, fwd), across = Vector3.Dot(d, side);
                if (Mathf.Abs(across) > half || along < from - 0.3f || along > face + 0.7f) continue;
                if (Mathf.Abs(e.bodies[0].position.y - rankStart.y) > s.rankHeight + 0.5f) continue;
                if (blow)
                {
                    e.Kick(fwd, s.rankJump + 0.5f, 0.25f, false);
                    e.LastSkillHit = "7번째 줄: 벽에 받혀 튕겨 나감";
                    n++;
                    continue;
                }
                float v = Vector3.Dot(Flat(e.bodies[0].linearVelocity), fwd), want = speed + s.rankPushOver;
                if (v < want) e.AddVelocity(fwd * (want - v));
            }
            if (blow)
            {
                FeelHit(null, planPoint, fwd, FeelHard, Mathf.Max(1, n), false);
                Fx(SkillFxKind.RookSlam, null, planPoint, fwd, n);
                Log($"7번째 줄: 모아 치기 — 벽이 튀어 나감, 앞의 적 {n}명");
            }
        }

        void EndSeventhRank(PawnRushSkillParams s, string why)
        {
            if (rankWall != null)
            {
                rankWall.Break();
                TellWallBroken(rankWall);
                rankWall = null;
            }
            float went = rankAlong;
            EndPlanSkill();
            StartCooldown(UltCooldown(activeSkill, s));
            Log($"{DisplayName}: 7번째 줄 끝 ({why}, {went:0.#} m 밀고 감)");
        }

        /// <summary>A 7th rank cut short (a knockdown, a reset): its wall goes down with it.</summary>
        void ClearSeventhRank()
        {
            if (rankWall == null) return;
            rankWall.Break();
            TellWallBroken(rankWall);
            rankWall = null;
        }
    }
}
