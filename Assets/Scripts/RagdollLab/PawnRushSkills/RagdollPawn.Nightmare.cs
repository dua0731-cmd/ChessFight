using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// N-궁 나이트메어 (KNIGHTMARE; JY-skill-lab, 2026-11-22, Docs/Skills/PAWN_RUSH_SKILL_KITS.md 6.2): the only piece that
    /// jumps over heads, the fork and the knight's tour - for 10 s (×1.5 15, ×2 20) the knight leaps from head to head.
    ///
    ///   aim      the camera's look on the floor within the leap's reach; on an enemy's red mark (within 4 m of it, open
    ///            sky over it) it fastens on that enemy. Under a roof or a bridge there is nowhere to come down.
    ///   leap     the left click: high (7 m + 0.14 m a metre) and long; the first 40 m, after a stomp 60 m, after two in a
    ///            row 80 m. In the air the move keys shift the landing up to 7 m; fastened, the landing follows its enemy
    ///            until the last 0.5 s (the red circle every screen sees: a quick enemy can still get out).
    ///   land     every enemy within 3 m of the landing is stomped (two or more: a fork, all of them): 2 s flat, then
    ///            thrown 15 m back. A stomp lengthens the next leap; an empty landing takes it back to 40 m. On a head the
    ///            knight stands on it (up to 1.2 s) until the next click. An enemy is stomped once an ultimate.
    ///   end      its time out (a leap in the air comes down first): the judgement by how many it stomped.
    ///
    /// planNote: the level (0..2: 40, 60, 80 m), and NmBlocked, NmSnapped, NmFlying, NmRiding. planPoint the aimed or
    /// landing floor point, planTarget the enemy it is fastened on. Not seen in Unity yet.
    /// </summary>
    public partial class RagdollPawn
    {
        public const byte NmLevelMask = 3, NmBlocked = 4, NmSnapped = 8, NmFlying = 16, NmRiding = 32;

        int nmLevel;
        bool nmFlying;
        float nmRideLeft;
        Vector3 nmBase, nmOffset;
        static readonly List<RagdollPawn> nmScratch = new List<RagdollPawn>();

        /// <summary>The leap's reach at a level (0 the first, 1 after a stomp, 2 after two in a row).</summary>
        public static float NmRange(PawnRushSkillParams s, int level) => level <= 0 ? s.nmRange0 : level == 1 ? s.nmRange1 : s.nmRange2;

        /// <summary>
        /// Where the knight would come down (the host's rule, and the knight's own screen's from the same look): the floor
        /// point the camera looks at within <paramref name="reach"/>, fastened onto a standing enemy within nmSnap of it
        /// (within reach, open sky over it, not in <paramref name="skip"/>). <paramref name="blocked"/>: no sky over the
        /// point (nowhere to come down).
        /// </summary>
        public static Vector3 NmAim(RagdollPawn knight, Vector3 aim, float reach, PawnRushSkillParams s, ICollection<RagdollPawn> skip,
                                    out RagdollPawn snapped, out bool blocked)
        {
            snapped = null;
            blocked = false;
            if (knight == null || s == null) return Vector3.zero;
            Vector3 point = DriveAimPoint(knight, aim, reach, out _), me = knight.FeetPoint;
            float best = s.nmSnap;
            foreach (var e in All)
            {
                if (!knight.UltFoe(e) || skip != null && skip.Contains(e)) continue;
                Vector3 feet = e.FeetPoint;
                float d = Flat(feet - point).magnitude;
                if (d > best || Mathf.Abs(feet.y - point.y) > 4f || Flat(feet - me).magnitude > reach + s.nmSnap) continue;
                if (!SkyOver(feet, s.nmSky)) continue;
                best = d;
                snapped = e;
            }
            if (snapped != null) return snapped.FeetPoint;
            blocked = !SkyOver(point, s.nmSky);
            return point;
        }

        void UpdateNightmare(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill(true)) break;
                    BeginDrive(s, PawnRushSkillIds.Nightmare, s.nmSeconds, 0, "나이트메어 — 좌클릭으로 착지점 (빨간 표식에 붙음)");
                    nmLevel = 0;
                    nmFlying = false;
                    nmRideLeft = 0f;
                    ultHead = null;
                    NmNote(false);
                    break;
                case PlanPhase.Active:
                    planTime += dt;
                    if (nmFlying)
                    {
                        // A click early in the flight is spent; one in its last quarter second goes on landing.
                        if (ultFlightTime - ultFlightT > 0.25f) TakeClick();
                        if (NmFly(s, dt)) NmLand(s);
                        NmNote(false);
                        break;
                    }
                    if (nmRideLeft > 0f)
                    {
                        nmRideLeft -= dt;
                        if (nmRideLeft > 0f && ultHead != null && ultHead.State == PawnState.Active) TourRide(dt);
                        else NmStepDown();
                    }
                    if (DriveLeft <= 0f)
                    {
                        if (nmRideLeft > 0f) NmStepDown();
                        FinishDrive(s);
                        break;
                    }
                    planPoint = NmAim(this, AimDir(), NmRange(s, nmLevel), s, driveHit, out var snapped, out bool blocked);
                    planTarget = snapped;
                    planDir = FlatDir(planPoint - FeetPoint);
                    NmNote(blocked);
                    if (!TakeClick()) break;
                    if (blocked)
                    {
                        Fx(SkillFxKind.SkillFail, null, planPoint, planDir, (int)PieceKind.Knight);
                        Log("나이트메어: 하늘이 막힌 곳에는 못 내려섬");
                        break;
                    }
                    NmLeap(s);
                    NmNote(false);
                    break;
                case PlanPhase.Recovery:
                    UpdateDriveRecovery(s, dt);
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        void NmNote(bool blocked)
        {
            planNote = (byte)(Mathf.Clamp(nmLevel, 0, 2) | (blocked ? NmBlocked : 0) | (planTarget != null ? NmSnapped : 0)
                              | (nmFlying ? NmFlying : 0) | (nmRideLeft > 0f ? NmRiding : 0));
        }

        /// <summary>Where the hips come down: on the enemy's head when fastened (and not steered off it), else over the floor.</summary>
        Vector3 NmLandingHips()
        {
            if (planTarget != null && nmOffset.magnitude < 1f) return planTarget.bodies[(int)BodyId.Head].position + Vector3.up * 0.35f;
            return planPoint + Vector3.up * standHeight;
        }

        /// <summary>How high an arc from <paramref name="from"/> to <paramref name="to"/> may rise under what is over it (a
        /// ceiling, a bridge): at least 1.5 m.</summary>
        static float NmHeadroom(Vector3 from, Vector3 to)
        {
            float room = 60f;
            for (int i = 0; i <= 2; i++)
            {
                Vector3 p = Vector3.Lerp(from, to, i * 0.5f) + Vector3.up * 0.5f;
                foreach (var h in Physics.RaycastAll(p, Vector3.up, 60f, ~0, QueryTriggerInteraction.Ignore))
                    if (h.collider.GetComponentInParent<RagdollPawn>() == null) room = Mathf.Min(room, h.distance - 1.5f);
            }
            return Mathf.Max(1.5f, room);
        }

        void NmLeap(PawnRushSkillParams s)
        {
            nmRideLeft = 0f;
            DropHook();
            DropRope();
            EndClimbPose();
            LetGo(0.3f);
            nmBase = planPoint;
            nmOffset = Vector3.zero;
            ultHead = planTarget;
            ultFrom = bodies[0].position;
            ultTo = NmLandingHips();
            float across = Flat(ultTo - ultFrom).magnitude;
            ultFlightT = 0f;
            ultFlightTime = s.nmAir + across / 100f * s.nmAirPer100;
            ultFlightTop = Mathf.Min(s.nmHigh + across * s.nmHighPer, NmHeadroom(ultFrom, ultTo));
            nmFlying = true;
            Fx(SkillFxKind.UltGo, planTarget, planPoint, planDir, PawnRushSkillIds.Nightmare, ultFlightTime);
            Log($"나이트메어: {across:0} m 도약" + (planTarget != null ? $" → {planTarget.DisplayName}" : ""));
        }

        /// <summary>The flight this step: the move keys shift the landing, a fastened landing follows its enemy until the
        /// last moment; true once it is down.</summary>
        bool NmFly(PawnRushSkillParams s, float dt)
        {
            ultFlightT += dt;
            grabImmune = Mathf.Max(grabImmune, 0.25f);
            float left = ultFlightTime - ultFlightT;
            Vector3 keys = Flat(skillMoveRaw);
            if (left > 0.12f && keys.sqrMagnitude > 0.01f)
            {
                nmOffset += (keys.sqrMagnitude > 1f ? keys.normalized : keys) * (s.nmSteer * dt);
                if (nmOffset.magnitude > s.nmSteerMax) nmOffset = nmOffset.normalized * s.nmSteerMax;
            }
            var target = planTarget;
            if (target != null && (target.State != PawnState.Active || target.Floating || nmOffset.magnitude > s.nmStomp)) planTarget = target = null;
            if (target != null && left > s.nmLock) nmBase = Vector3.MoveTowards(nmBase, target.FeetPoint, s.nmHome * dt);
            Vector3 spot = nmBase + nmOffset;
            planPoint = DriveFloor(spot, 6f, 12f) ?? spot;
            // Past the lock the spot stays: an enemy that got out of its circle is no longer under the knight.
            if (target != null && left <= s.nmLock && Flat(target.FeetPoint - planPoint).magnitude > s.nmStomp) planTarget = null;
            ultHead = planTarget;
            ultTo = Vector3.MoveTowards(ultTo, NmLandingHips(), 40f * dt);
            if (ultFlightT >= ultFlightTime) return true;
            Vector3 at = bodies[0].position, next = UltArc((ultFlightT + dt) / ultFlightTime);
            Fly(next, (next - at) / Mathf.Max(1e-4f, dt), 0f, default, 18f);
            return false;
        }

        void NmLand(PawnRushSkillParams s)
        {
            nmFlying = false;
            // Where it really came down (a wall taller than the arc can stop it short of the spot), on the spot's floor.
            Vector3 hips = bodies[0].position, at = new Vector3(hips.x, planPoint.y, hips.z);
            if (Flat(at - planPoint).magnitude > 2f) at = DriveFloor(hips, 2f, 20f) ?? at;
            nmScratch.Clear();
            foreach (var e in All)
            {
                if (!DriveFoe(e)) continue;
                Vector3 feet = e.FeetPoint;
                if (Flat(feet - at).magnitude > s.nmStomp || Mathf.Abs(feet.y - at.y) > 3f) continue;
                nmScratch.Add(e);
            }
            if (nmScratch.Count == 0)
            {
                EndFlight(Vector3.zero);
                KeepSkillLandingSafe();
                ultHead = null;
                nmLevel = 0;
                Fx(SkillFxKind.DriveLand, null, at, planDir, 0, 0f);
                FeelMiss(at, planDir);
                Log($"나이트메어: 빈 땅 — 다음 도약 {NmRange(s, 0):0} m");
                return;
            }
            bool fork = nmScratch.Count >= 2;
            foreach (var e in nmScratch) NmStomp(s, e, at, fork);
            nmLevel = Mathf.Min(2, nmLevel + 1);
            Fx(SkillFxKind.DriveLand, null, at, planDir, nmScratch.Count, nmLevel);
            FeelHit(null, at, planDir, FeelUltimate, nmScratch.Count, false);
            if (ultHead != null && nmScratch.Contains(ultHead)) nmRideLeft = s.nmRide;   // standing on its head
            else
            {
                EndFlight(Vector3.zero);
                KeepSkillLandingSafe();
                ultHead = null;
            }
            Log($"나이트메어: {(fork ? $"포크! {nmScratch.Count}명" : "밟음")} — 다음 도약 {NmRange(s, nmLevel):0} m");
        }

        void NmStomp(PawnRushSkillParams s, RagdollPawn e, Vector3 at, bool fork)
        {
            driveHit.Add(e);
            ultTaken.Add(e);
            if (e.Squash(s.nmSquash, 0f)) e.GaugeKnocked(this);
            Vector3 away = Flat(e.FeetPoint - at);
            away = away.sqrMagnitude > 0.04f ? away.normalized : FlatDir(e.facing);
            e.slideAt = Time.time + s.nmSquash;
            e.slideDir = FlatDir(away * 0.6f - RunWay(e));
            e.slideMetres = s.nmBounce;
            e.LastSkillHit = $"나이트메어: {(fork ? "포크! " : "")}납작 {s.nmSquash:0.#}초, {s.nmBounce:0} m 튕김";
            Vector3 head = e.bodies[(int)BodyId.Head].position;
            Fx(SkillFxKind.UltStrike, e, head, planDir, driveHit.Count, fork ? 1f : 0f);
            FeelHit(e, head, planDir, FeelHard, driveHit.Count, true);
        }

        /// <summary>Off the head it stood on, onto the floor.</summary>
        void NmStepDown()
        {
            nmRideLeft = 0f;
            ultHead = null;
            EndFlight(Vector3.zero);
            KeepSkillLandingSafe();
        }

        void ClearNightmare()
        {
            if (nmFlying || nmRideLeft > 0f) EndFlight();
            nmFlying = false;
            nmRideLeft = 0f;
            nmLevel = 0;
        }
    }
}
