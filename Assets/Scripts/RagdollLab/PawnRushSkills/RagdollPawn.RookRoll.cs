using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R-궁 룩앤롤, Rook'n'Roll (JY-skill-lab, 2026-11-22, Docs/Skills/PAWN_RUSH_SKILL_KITS.md 6.4): the rook goes in a
    /// straight line to the end - here the whole tower, toppled, rolling. For 10 s (×1.5 15, ×2 20):
    ///
    ///   the roll    a stone column 3 m across (4.5 m long, across the way it rolls); the move keys steer it (140°/s slow,
    ///               70°/s fast). Straight on it gathers speed, 15 to 40 m/s in 3 s; a turn costs speed (0.12 m/s a
    ///               degree, 10 m/s at least). A wall throws it back off it at half speed; the rook's own walls and the
    ///               barricades it breaks, loose things it knocks away. Steps up to 1.2 m it climbs; off an edge it falls.
    ///   the jump    the jump key once: a big bounce (14 m/s up) to come down on a high place.
    ///   the pins    an enemy it rolls into is thrown 25 m (on, and out to its side) and floored 2 s; four within 0.6 s is
    ///               a strike ("스트라이크!").
    ///   a lift      a teammate that jumps onto it is thrown 15 m ahead (on its feet), once.
    ///   the end     its time out: the tower stands up, and the judgement.
    ///
    /// planNote: its speed (0..15 of the top speed), RrJumped, RrAir. planDir the way it rolls; the rolled body is the
    /// rook's hips, carried at the column's middle. Not seen in Unity yet.
    /// </summary>
    public partial class RagdollPawn
    {
        public const byte RrSpeedMask = 15, RrJumped = 16, RrAir = 32;
        public const int RrTopple = 1, RrJump = 2, RrLift = 3, RrStand = 4, RrStrike = 5, RrCrash = 6;

        bool rrRolling, rrJumped, rrAir;
        float rrSpeed, rrVy, rrStrikeAt;
        int rrStrikeRun;
        Vector3 rrCenter;
        readonly HashSet<RagdollPawn> rrLifted = new HashSet<RagdollPawn>();

        void UpdateRookRoll(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill(true)) break;
                    BeginDrive(s, PawnRushSkillIds.RookRoll, s.rrSeconds, 0, "룩앤롤 — 이동 키로 몰기, 점프 = 크게 튀기 (한 번)");
                    DropHook();
                    DropRope();
                    EndClimbPose();
                    LetGo(0.3f);
                    rrRolling = true;
                    rrJumped = rrAir = false;
                    rrSpeed = s.rrSpeed0;
                    rrVy = 0f;
                    rrStrikeRun = 0;
                    rrStrikeAt = -10f;
                    rrLifted.Clear();
                    planDir = MoveOrAim();
                    rrCenter = (DriveFloor(FeetPoint, 1f, 4f) ?? FeetPoint) + Vector3.up * s.rrRadius;
                    Fx(SkillFxKind.DriveState, null, FeetPoint, planDir, RrTopple, s.rrRadius);
                    RrNote(s);
                    break;
                case PlanPhase.Active:
                    planTime += dt;
                    TakeClick();
                    TakeRight();
                    if (rrRolling) RrStep(s, dt);
                    if (DriveLeft <= 0f && !rrAir)
                    {
                        RrStandUp(s);
                        FinishDrive(s);
                        break;
                    }
                    RrNote(s);
                    break;
                case PlanPhase.Recovery:
                    UpdateDriveRecovery(s, dt);
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        void RrNote(PawnRushSkillParams s)
        {
            int level = Mathf.Clamp(Mathf.RoundToInt(rrSpeed / Mathf.Max(1f, s.rrSpeed1) * 15f), 0, 15);
            planNote = (byte)(level | (rrJumped ? RrJumped : 0) | (rrAir ? RrAir : 0));
        }

        void RrStep(PawnRushSkillParams s, float dt)
        {
            grabImmune = Mathf.Max(grabImmune, 0.25f);
            // Steering: straight on it gathers speed, a turn costs it.
            Vector3 keys = Flat(skillMoveRaw), want = keys.sqrMagnitude > 0.04f ? keys.normalized : planDir;
            float fast = Mathf.InverseLerp(s.rrSpeed0, s.rrSpeed1, rrSpeed);
            float maxTurn = Mathf.Lerp(s.rrTurnSlow, s.rrTurnFast, fast) * Mathf.Deg2Rad * dt;
            Vector3 next = rrAir ? planDir : Vector3.RotateTowards(planDir, want, maxTurn, 0f);
            float turned = Vector3.Angle(planDir, next);
            planDir = FlatDir(next);
            if (!rrAir)
            {
                if (turned / Mathf.Max(1e-4f, dt) < 20f) rrSpeed += (s.rrSpeed1 - s.rrSpeed0) / Mathf.Max(0.1f, s.rrRamp) * dt;
                else rrSpeed -= turned * s.rrTurnCost;
                rrSpeed = Mathf.Clamp(rrSpeed, s.rrSpeedMin, s.rrSpeed1);
            }
            if (!rrAir && TakeJump() && !rrJumped)
            {
                rrJumped = rrAir = true;
                rrVy = s.rrJumpUp;
                Fx(SkillFxKind.DriveState, null, rrCenter, planDir, RrJump, rrSpeed);
            }
            // Ahead: a wall (thrown back off it), a wall of the rook's own or a barricade (broken), a loose thing (away).
            float go = rrSpeed * dt;
            Vector3 move = planDir * go;
            if (RrBlocked(s, rrCenter, planDir, go + 0.3f, out var hit))
            {
                var kitWall = hit.collider.GetComponentInParent<PawnRushKitWall>();
                var barricade = hit.collider.GetComponentInParent<SkillBarricade>();
                if (kitWall != null) kitWall.Break();
                else if (barricade != null) barricade.Break(s.rrRegrow);
                else if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
                    hit.rigidbody.AddForce((planDir * 0.8f + Vector3.up * 0.4f) * rrSpeed * hit.rigidbody.mass, ForceMode.Impulse);
                else
                {
                    Vector3 wall = Flat(hit.normal);
                    if (wall.sqrMagnitude > 1e-4f) planDir = FlatDir(Vector3.Reflect(planDir, wall.normalized));
                    Fx(SkillFxKind.DriveState, null, hit.point, planDir, RrCrash, rrSpeed);
                    rrSpeed = Mathf.Max(s.rrSpeedMin, rrSpeed * 0.5f);
                    move = Vector3.zero;
                }
            }
            Vector3 center = rrCenter + move;
            // The floor under it (looked for from 1.2 m over its middle, so a beam over the way is not taken for a step):
            // climbed up a step of 1.2 m at most, fallen off an edge, landed from the jump.
            var floor = DriveFloor(center, 1.2f, 30f);
            float rest = floor.HasValue ? floor.Value.y + s.rrRadius : float.NegativeInfinity;
            if (!rrAir && floor.HasValue && rest - center.y > 1.2f)
            {
                // Too high a step: as a wall.
                center = rrCenter;
                rest = center.y;
                rrSpeed = Mathf.Max(s.rrSpeedMin, rrSpeed * 0.5f);
                planDir = -planDir;
                Fx(SkillFxKind.DriveState, null, rrCenter + planDir * s.rrRadius, planDir, RrCrash, rrSpeed);
            }
            if (rrAir)
            {
                rrVy -= s.rrGravity * dt;
                center.y += rrVy * dt;
                if (rrVy <= 0f && center.y <= rest)
                {
                    center.y = rest;
                    rrAir = false;
                    rrVy = 0f;
                    Fx(SkillFxKind.DriveState, null, center - Vector3.up * s.rrRadius, planDir, RrCrash, rrSpeed);
                }
            }
            else if (center.y - rest > 1f)
            {
                rrAir = true;   // off an edge
                rrVy = 0f;
            }
            else if (floor.HasValue) center.y = rest;
            Vector3 hips = bodies[0].position;
            Fly(center, (center - hips) / Mathf.Max(1e-4f, dt), 0f, default, 22f);
            rrCenter = center;
            RrHits(s);
        }

        /// <summary>The first solid thing (not a piece) the column meets along <paramref name="way"/>.</summary>
        static bool RrBlocked(PawnRushSkillParams s, Vector3 center, Vector3 way, float length, out RaycastHit first)
        {
            first = default;
            float best = float.MaxValue;
            // (Its middle and both ends: the column is long across the way it rolls.)
            Vector3 right = new Vector3(way.z, 0f, -way.x);
            for (int i = -1; i <= 1; i++)
            {
                Vector3 from = center + right * (i * s.rrLength * 0.4f) + Vector3.up * 0.3f;
                foreach (var h in Physics.SphereCastAll(from, s.rrRadius * 0.6f, way, length, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (h.distance >= best || h.distance <= 0f || h.collider.GetComponentInParent<RagdollPawn>() != null) continue;
                    if (h.normal.y >= 0.6f) continue;   // a floor or a slope under it, not a wall
                    best = h.distance;
                    first = h;
                }
            }
            return best < float.MaxValue;
        }

        /// <summary>Whoever the column rolls into: enemies thrown like pins, a teammate on top thrown ahead.</summary>
        void RrHits(PawnRushSkillParams s)
        {
            Vector3 right = new Vector3(planDir.z, 0f, -planDir.x);
            float half = s.rrLength * 0.5f, r = s.rrRadius;
            foreach (var e in All)
            {
                if (e == null || e == this || e.State != PawnState.Active) continue;
                Vector3 p = e.bodies[0].position, local = p - rrCenter;
                float along = Vector3.Dot(local, planDir), across = Vector3.Dot(local, right), up = local.y;
                if (Mathf.Abs(across) > half + 0.45f) continue;
                if (IsEnemyOf(e))
                {
                    if (!DriveFoe(e) || Mathf.Abs(along) > r + 0.45f || up < -r - 0.6f || up > r + 0.6f) continue;
                    driveHit.Add(e);
                    float side = across >= 0f ? 1f : -1f;
                    Vector3 away = FlatDir(planDir + right * side * Random.Range(0.5f, 1.1f));
                    var to = DriveFloor(e.FeetPoint + away * s.rrThrow, 5f, 15f) ?? e.FeetPoint;
                    Fx(SkillFxKind.UltStrike, e, p, away, driveHit.Count, rrSpeed);
                    e.UltThrow(to, 0.9f, 6f, s.rrKnock, this);
                    e.LastSkillHit = $"룩앤롤: 볼링 핀처럼 {s.rrThrow:0} m";
                    FeelHit(e, p, planDir, FeelHard, driveHit.Count, true);
                    if (Time.time - rrStrikeAt > s.rrStrikeWindow) rrStrikeRun = 0;
                    rrStrikeAt = Time.time;
                    rrStrikeRun++;
                    if (rrStrikeRun == s.rrStrike)
                    {
                        Fx(SkillFxKind.DriveState, null, rrCenter, planDir, RrStrike, rrStrikeRun);
                        Log($"룩앤롤: 스트라이크! ({rrStrikeRun})");
                    }
                }
                else if (IsAllyOf(e) && !rrLifted.Contains(e) && up > r * 0.4f && up < r + 2.5f && Mathf.Abs(along) <= r + 0.6f)
                {
                    rrLifted.Add(e);
                    var to = DriveFloor(e.FeetPoint + planDir * s.rrLift, 5f, 15f) ?? e.FeetPoint + planDir * s.rrLift;
                    e.UltThrow(to, 0.9f, 5f, 0f, this);
                    Fx(SkillFxKind.DriveState, e, p, planDir, RrLift, s.rrLift);
                    Log($"룩앤롤: {e.DisplayName}를 앞으로 {s.rrLift:0} m");
                }
            }
        }

        void RrStandUp(PawnRushSkillParams s)
        {
            if (!rrRolling) return;
            rrRolling = false;
            EndFlight(planDir * Mathf.Min(rrSpeed, 6f));
            KeepSkillLandingSafe();
            Fx(SkillFxKind.DriveState, null, FeetPoint, planDir, RrStand, 0f);
        }

        void ClearRookRoll()
        {
            if (!rrRolling) return;
            rrRolling = false;
            rrAir = false;
            EndFlight();
        }
    }
}
