using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// K-궁 킹 워크 (JY-skill-lab, 2026-11-22, Docs/Skills/PAWN_RUSH_SKILL_KITS.md 6.6): in the endgame the king is a fighting
    /// piece - Short–Timman 1991, the king walking up the board to mate. For 12 s (×1.5 18, ×2 24):
    ///
    ///   the walk       the king is drawn 1.5x his size and walks 1.8x as fast; nothing floors him; an enemy in his way
    ///                  is shoved aside.
    ///   the carpet     a red carpet 100 m long behind him: at the key straight back from where he stands (on the floors
    ///                  it meets), then the way he walks; it keeps its 100 m. Every PC lays the same one from the cast's
    ///                  moment and the king's walk (KwCarpetPoint).
    ///   the train      a teammate on the carpet is pulled along it to the king (40 m/s) and walks in his train, 2.5 m
    ///                  apart, at his speed; knocked over or pushed off its place (3 m), it leaves the train.
    ///   the majority   the king and three in his train (four of six): he grows again (1.8x) and walks 2.2x as fast.
    ///   the crown      the left click: 0.4 s of warning (a fan on the floor ahead), then a shock wave through the 30 m
    ///                  fan ahead (70°): enemies in it thrown 15 m and floored 2 s; 1 s between, three (×1.5 four, ×2 six).
    ///   the end        his time out: his size back, the train set down, and the judgement (enemies hit and teammates
    ///                  brought both count).
    ///
    /// planNote: the waves left (low four bits), the train (KwTrainShift, 0..7), KwGrown. Not seen in Unity yet.
    /// </summary>
    public partial class RagdollPawn
    {
        public const byte KwWavesMask = 15, KwTrainShift = 4, KwTrainMask = 7, KwGrown = 128;

        bool kwOn, kwGrown, kwWinding;
        float kwScale = 1f, kwShoveAt, kwWaveT, kwWaveGap;
        int kwBrought;
        Vector3 kwWaveDir;
        readonly List<Vector3> kwCarpet = new List<Vector3>();
        readonly List<RagdollPawn> kwTrain = new List<RagdollPawn>();
        readonly Dictionary<RagdollPawn, float> kwPulling = new Dictionary<RagdollPawn, float>();
        static readonly List<RagdollPawn> kwScratch = new List<RagdollPawn>();

        /// <summary>The king's walk's speed on the run now (SkillMoveScale).</summary>
        float KingWalkScale => kwOn && planPhase == PlanPhase.Active ? kwScale : 1f;

        /// <summary>
        /// The carpet at the key (any PC the same): from <paramref name="feet"/> straight back against <paramref name="way"/>,
        /// a point every 2 m on the floor it meets, up to <paramref name="length"/> m; a wall or a drop of over 3 m ends it.
        /// </summary>
        public static void KwLayCarpet(Vector3 feet, Vector3 way, float length, List<Vector3> into)
        {
            into.Clear();
            into.Add(feet);
            Vector3 back = -FlatDir(way), last = feet;
            for (float d = 2f; d <= length; d += 2f)
            {
                Vector3 p = feet + back * d;
                var floor = DriveFloor(new Vector3(p.x, last.y, p.z), 3f, 3f);
                if (!floor.HasValue) break;
                if (GdWall(last + Vector3.up * 0.8f, FlatDir(floor.Value - last), Flat(floor.Value - last).magnitude, out _)) break;
                into.Add(floor.Value);
                last = floor.Value;
            }
        }

        /// <summary>The king's new floor point at the carpet's front (once he is 0.5 m on), the tail kept to the length.</summary>
        public static void KwExtendCarpet(List<Vector3> carpet, Vector3 kingFeet, float length)
        {
            if (carpet.Count == 0 || Flat(kingFeet - carpet[0]).magnitude >= 0.5f) carpet.Insert(0, kingFeet);
            float run = 0f;
            for (int i = 1; i < carpet.Count; i++)
            {
                run += Vector3.Distance(carpet[i - 1], carpet[i]);
                if (run <= length) continue;
                carpet.RemoveRange(i + 1 < carpet.Count ? i + 1 : carpet.Count, Mathf.Max(0, carpet.Count - (i + 1)));
                break;
            }
        }

        /// <summary>The point <paramref name="along"/> m back along the carpet from the king, and whether it is that long.</summary>
        public static Vector3 KwCarpetPoint(List<Vector3> carpet, float along)
        {
            if (carpet.Count == 0) return Vector3.zero;
            float run = 0f;
            for (int i = 1; i < carpet.Count; i++)
            {
                float len = Vector3.Distance(carpet[i - 1], carpet[i]);
                if (along <= run + len) return Vector3.Lerp(carpet[i - 1], carpet[i], len > 1e-4f ? (along - run) / len : 0f);
                run += len;
            }
            return carpet[carpet.Count - 1];
        }

        /// <summary>How far back along the carpet <paramref name="p"/> stands, if it stands on it (within its half width).</summary>
        public static bool KwOnCarpet(List<Vector3> carpet, Vector3 p, float halfWidth, out float along)
        {
            along = 0f;
            float run = 0f, best = float.MaxValue;
            bool on = false;
            for (int i = 1; i < carpet.Count; i++)
            {
                Vector3 a = carpet[i - 1], b = carpet[i], ab = Flat(b - a);
                float len = ab.magnitude;
                if (len < 1e-4f) continue;
                float t = Mathf.Clamp(Vector3.Dot(Flat(p - a), ab / len), 0f, len);
                Vector3 near = Vector3.Lerp(a, b, t / len);
                float off = Flat(p - near).magnitude;
                if (off <= halfWidth && Mathf.Abs(p.y - near.y) <= 1.6f && off < best)
                {
                    best = off;
                    along = run + t;
                    on = true;
                }
                run += len;
            }
            return on;
        }

        void UpdateKingWalk(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill(true)) break;
                    BeginDrive(s, PawnRushSkillIds.KingWalk, s.kwSeconds, s.kwWaves, "킹 워크 — 이동 키로 행진, 좌클릭 = 왕관 충격파");
                    kwOn = true;
                    kwGrown = kwWinding = false;
                    kwScale = s.kwSpeed;
                    kwShoveAt = 0f;
                    kwWaveGap = 0f;
                    kwBrought = 0;
                    kwTrain.Clear();
                    kwPulling.Clear();
                    KwLayCarpet(FeetPoint, planDir, s.kwCarpet, kwCarpet);
                    KwNote();
                    break;
                case PlanPhase.Active:
                    planTime += dt;
                    getUpGuardLeft = Mathf.Max(getUpGuardLeft, 0.25f);   // nothing floors him
                    planDir = FlatDir(facing);
                    KwExtendCarpet(kwCarpet, FeetPoint, s.kwCarpet);
                    KwShoveAhead(s, dt);
                    KwTrainStep(s, dt);
                    KwWaveStep(s, dt);
                    if (DriveLeft <= 0f && !kwWinding)
                    {
                        KwRelease();
                        FinishDrive(s, kwBrought);
                        break;
                    }
                    KwNote();
                    break;
                case PlanPhase.Recovery:
                    UpdateDriveRecovery(s, dt);
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        void KwNote()
        {
            planNote = (byte)(Mathf.Clamp(driveUses, 0, 15) | (Mathf.Clamp(kwTrain.Count, 0, KwTrainMask) << KwTrainShift) | (kwGrown ? KwGrown : 0));
        }

        /// <summary>An enemy in his way is shoved aside (every 0.2 s).</summary>
        void KwShoveAhead(PawnRushSkillParams s, float dt)
        {
            if ((kwShoveAt -= dt) > 0f) return;
            kwShoveAt = 0.2f;
            Vector3 me = bodies[0].position, fwd = planDir, right = new Vector3(fwd.z, 0f, -fwd.x);
            float keep = s.kwKeep * (kwGrown ? s.kwGrowBig : s.kwGrow);
            foreach (var p in All)
            {
                if (!UltFoe(p)) continue;
                Vector3 d = Flat(p.bodies[0].position - me);
                if (d.magnitude > keep || Vector3.Dot(d, fwd) < -0.5f) continue;
                float side = Vector3.Dot(d, right) >= 0f ? 1f : -1f;
                p.Kick(FlatDir(right * side + fwd * 0.3f), Mathf.Max(0.5f, keep + 0.6f - d.magnitude), 0.15f, false);
                Fx(SkillFxKind.PawnHit, p, p.bodies[0].position, right * side, 0);
            }
        }

        /// <summary>The carpet's riders pulled to the king, his train carried behind him.</summary>
        void KwTrainStep(PawnRushSkillParams s, float dt)
        {
            // Who steps on the carpet now.
            foreach (var p in All)
            {
                // (Not one carried by something else already, or in a skill of its own.)
                if (p == null || p == this || !IsAllyOf(p) || p.State != PawnState.Active || p.Floating || p.OuterFlying || p.SkillRunning) continue;
                if (kwTrain.Contains(p) || kwPulling.ContainsKey(p)) continue;
                if (!KwOnCarpet(kwCarpet, p.FeetPoint, s.kwHalfWidth, out float along) || along < s.kwGap * 0.5f) continue;
                kwPulling[p] = along;
                Fx(SkillFxKind.DriveState, p, p.FeetPoint, planDir, 1, along);
                Log($"킹 워크: {p.DisplayName} 카펫에 — 행렬로");
            }
            // Pulled along it.
            kwScratch.Clear();
            kwScratch.AddRange(kwPulling.Keys);
            foreach (var p in kwScratch)
            {
                if (p == null || p.State != PawnState.Active)
                {
                    kwPulling.Remove(p);
                    continue;
                }
                float slot = s.kwGap * (kwTrain.Count + 1), along = Mathf.MoveTowards(kwPulling[p], slot, s.kwPullSpeed * dt);
                kwPulling[p] = along;
                KwCarry(p, along, dt);
                if (along > slot + 0.01f) continue;
                kwPulling.Remove(p);
                kwTrain.Add(p);
                kwBrought = Mathf.Max(kwBrought, kwTrain.Count);
                Fx(SkillFxKind.DriveState, p, p.FeetPoint, planDir, 2, kwTrain.Count);
                if (!kwGrown && kwTrain.Count >= s.kwMajority)
                {
                    kwGrown = true;
                    kwScale = s.kwSpeedBig;
                    Fx(SkillFxKind.DriveState, null, FeetPoint, planDir, 3, kwTrain.Count + 1);
                    Log($"킹 워크: 과반! 킹과 행렬 {kwTrain.Count + 1}명 — 더 커지고 {s.kwSpeedBig:0.#}배");
                }
            }
            // The train: each at its place behind him; knocked over or pushed off it, out of the train.
            for (int i = kwTrain.Count - 1; i >= 0; i--)
            {
                var p = kwTrain[i];
                Vector3 place = KwCarpetPoint(kwCarpet, s.kwGap * (i + 1));
                bool off = p == null || p.State != PawnState.Active || Flat(p.FeetPoint - place).magnitude > s.kwFallOff;
                if (off)
                {
                    if (p != null)
                    {
                        p.EndFlight();
                        Fx(SkillFxKind.DriveState, p, p.FeetPoint, planDir, 4, 0f);
                        Log($"킹 워크: {p.DisplayName} 행렬에서 떨어짐");
                    }
                    kwTrain.RemoveAt(i);
                    continue;
                }
                KwCarry(p, s.kwGap * (i + 1), dt);
            }
        }

        void KwCarry(RagdollPawn p, float along, float dt)
        {
            Vector3 at = p.bodies[0].position, to = KwCarpetPoint(kwCarpet, along) + Vector3.up * p.standHeight;
            p.Fly(to, (to - at) / Mathf.Max(1e-4f, dt) * 0.5f + Flat(bodies[0].linearVelocity), 0f, default, 16f);
            p.getUpGuardLeft = Mathf.Max(p.getUpGuardLeft, 0.25f);
            p.grabImmune = Mathf.Max(p.grabImmune, 0.25f);
        }

        void KwWaveStep(PawnRushSkillParams s, float dt)
        {
            kwWaveGap -= dt;
            if (kwWinding)
            {
                kwWaveT += dt;
                if (kwWaveT >= s.kwWarn) KwWave(s);
                return;
            }
            if (kwWaveGap > 0f || driveUses <= 0 || !TakeClick()) return;
            kwWinding = true;
            kwWaveT = 0f;
            kwWaveDir = AimFlat();
            Fx(SkillFxKind.DriveWarn, null, FeetPoint, kwWaveDir, 1, s.kwWaveReach);
        }

        void KwWave(PawnRushSkillParams s)
        {
            kwWinding = false;
            kwWaveGap = s.kwWaveGap;
            driveUses--;
            Vector3 me = FeetPoint;
            int n = 0;
            foreach (var e in All)
            {
                if (!DriveFoe(e)) continue;
                Vector3 d = Flat(e.FeetPoint - me);
                if (d.magnitude > s.kwWaveReach || d.magnitude < 0.3f || Vector3.Angle(kwWaveDir, d) > s.kwWaveHalf || Mathf.Abs(e.FeetPoint.y - me.y) > 4f) continue;
                driveHit.Add(e);
                n++;
                Vector3 away = d.normalized;
                var to = DriveFloor(e.FeetPoint + away * s.kwThrow, 4f, 12f) ?? e.FeetPoint;
                Fx(SkillFxKind.UltStrike, e, e.bodies[0].position, away, driveHit.Count, 0f);
                e.UltThrow(to, 0.7f, 4f, s.kwKnock, this);
                e.LastSkillHit = $"킹 워크: 왕관 충격파에 {s.kwThrow:0} m";
            }
            Fx(SkillFxKind.DriveLine, null, me, kwWaveDir, driveUses, s.kwWaveReach, me + kwWaveDir * s.kwWaveReach);
            FeelHit(null, me, kwWaveDir, FeelUltimate, Mathf.Max(1, n), false);
            Log($"킹 워크: 왕관 충격파 — 적 {n}명 (남은 {driveUses}번)");
        }

        /// <summary>The train set down at the end (on its feet, moving on with the king's speed).</summary>
        void KwRelease()
        {
            foreach (var p in kwTrain) if (p != null) p.EndFlight(Flat(bodies[0].linearVelocity));
            foreach (var p in kwPulling.Keys) if (p != null) p.EndFlight();
            kwTrain.Clear();
            kwPulling.Clear();
            kwOn = false;
        }

        void ClearKingWalk()
        {
            if (!kwOn) return;
            KwRelease();
        }
    }
}
