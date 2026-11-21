using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// B-궁 롱 다이애거널, a1–h8!! (JY-skill-lab, 2026-11-22, Docs/Skills/PAWN_RUSH_SKILL_KITS.md 6.3): the bishop reaches
    /// from one end of the board to the other on one diagonal. For 10 s (×1.5 15, ×2 20) it walks and shoots, three
    /// shots (×1.5 four, ×2 six); every enemy is outlined in red through walls on its screen.
    ///
    ///   charge   the left click held 0.5 s (walking, no sprint), let go: the shot. Let go sooner and nothing is spent.
    ///            While it gathers, everyone's screen shows the line it would take and a red aim on whoever stands on it.
    ///   the line light 300 m along the camera's way at chest height. A wall turns it a right angle - the diagonal's
    ///            turn, the side nearer the wall's mirror (head on: the side with more room) - twice at most; each turn
    ///            makes it thicker (1, 1.5, 2 m). A floor or a slope ends it.
    ///   hit      every enemy on it (hips within the width, 2.5 m up or down) is pulled 25 m back along the line towards
    ///            the bishop (never past it) and floored 2 s.
    ///   end      its time out, or a second after its last shot: the judgement by how many it skewered.
    ///
    /// planNote: the shots left (low four bits), GdCharging, GdCharged; planDir the way it aims (every PC works out the
    /// same line from it: GdPath). Not seen in Unity yet.
    /// </summary>
    public partial class RagdollPawn
    {
        public const byte GdShotsMask = 15, GdCharging = 16, GdCharged = 32;
        public const int GdBounces = 2;

        float gdHold, gdAfter;
        static readonly Vector3[] gdPoints = new Vector3[GdBounces + 2];
        static readonly List<(RagdollPawn p, float along)> gdHits = new List<(RagdollPawn, float)>();

        /// <summary>Where the line starts: the bishop's chest.</summary>
        public Vector3 GdMuzzle => bodies[(int)BodyId.Chest].position + Vector3.up * 0.15f;

        /// <summary>
        /// The line from <paramref name="from"/> along <paramref name="dir"/> (flat), <paramref name="length"/> m in all,
        /// turned a right angle by each wall it meets (GdBounces at most): its corners into <paramref name="points"/>
        /// (from, the turns, the end), their count back. Every PC finds the same line (walls do not move).
        /// </summary>
        public static int GdPath(Vector3 from, Vector3 dir, float length, Vector3[] points)
        {
            Vector3 p = from, d = FlatDir(dir);
            float left = length;
            points[0] = from;
            int n = 1;
            for (int turn = 0; ; turn++)
            {
                if (!GdWall(p, d, left, out var hit))
                {
                    points[n++] = p + d * left;
                    return n;
                }
                Vector3 end = p + d * Mathf.Max(0f, hit.distance - 0.25f);
                points[n++] = end;
                left -= hit.distance;
                if (turn >= GdBounces || hit.normal.y >= 0.6f || left < 2f || n >= points.Length) return n;
                Vector3 wall = Flat(hit.normal);
                Vector3 mirror = wall.sqrMagnitude > 1e-4f ? Vector3.Reflect(d, wall.normalized) : -d;
                Vector3 right = new Vector3(d.z, 0f, -d.x), leftWay = -right;
                float r = Vector3.Dot(right, mirror), l = Vector3.Dot(leftWay, mirror);
                d = Mathf.Abs(r - l) < 0.15f ? (GdRoom(end, right) >= GdRoom(end, leftWay) ? right : leftWay) : r > l ? right : leftWay;
                p = end;
            }
        }

        /// <summary>The first wall (not a piece, not a trigger) along a flat line, within <paramref name="length"/>.</summary>
        static bool GdWall(Vector3 from, Vector3 dir, float length, out RaycastHit wall)
        {
            wall = default;
            float best = float.MaxValue;
            foreach (var h in Physics.RaycastAll(from, dir, length, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.distance >= best || h.distance < 0.05f || h.collider.GetComponentInParent<RagdollPawn>() != null) continue;
                best = h.distance;
                wall = h;
            }
            return best < float.MaxValue;
        }

        static float GdRoom(Vector3 from, Vector3 dir) => GdWall(from, dir, 60f, out var h) ? h.distance : 60f;

        /// <summary>The line's width after <paramref name="turn"/> turns (thicker at each).</summary>
        public static float GdWidth(PawnRushSkillParams s, int turn) => s.gdWidth + s.gdWiden * turn;

        /// <summary>Whether <paramref name="p"/> stands on the line (any PC: the aim's red mark, the warning).</summary>
        public static bool GdOnLine(PawnRushSkillParams s, Vector3[] points, int count, Vector3 p, out float along)
        {
            along = 0f;
            float run = 0f;
            for (int i = 0; i + 1 < count; i++)
            {
                Vector3 a = points[i], b = points[i + 1], ab = Flat(b - a);
                float len = ab.magnitude;
                if (len < 0.01f) continue;
                Vector3 d = ab / len;
                float t = Mathf.Clamp(Vector3.Dot(Flat(p - a), d), 0f, len);
                Vector3 near = a + d * t;
                if (Flat(p - near).magnitude <= GdWidth(s, i) + 0.3f && Mathf.Abs(p.y - near.y) <= s.gdUpDown)
                {
                    along = run + t;
                    return true;
                }
                run += len;
            }
            return false;
        }

        /// <summary>The point <paramref name="back"/> m back from <paramref name="along"/> on the line, towards its start.</summary>
        static Vector3 GdBackOnLine(Vector3[] points, int count, float along, float back)
        {
            float want = Mathf.Max(0f, along - back), run = 0f;
            for (int i = 0; i + 1 < count; i++)
            {
                float len = Flat(points[i + 1] - points[i]).magnitude;
                if (want <= run + len) return Vector3.Lerp(points[i], points[i + 1], len > 0.01f ? (want - run) / len : 0f);
                run += len;
            }
            return points[count - 1];
        }

        void UpdateGreatDiagonal(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill(true)) break;
                    BeginDrive(s, PawnRushSkillIds.GreatDiagonal, s.gdSeconds, s.gdShots, "롱 다이애거널 — 좌클릭 0.5초 모았다 떼기");
                    gdHold = 0f;
                    gdAfter = -1f;
                    GdNote();
                    break;
                case PlanPhase.Active:
                {
                    planTime += dt;
                    TakeClick();   // (the press itself does nothing: holding and letting go does)
                    planDir = AimFlat();
                    if (gdAfter >= 0f)
                    {
                        gdAfter -= dt;
                        if (gdAfter < 0f) FinishDrive(s);
                        break;
                    }
                    if (driveHeld && driveUses > 0)
                    {
                        if (gdHold <= 0f) Log("롱 다이애거널: 모으는 중");
                        gdHold += dt;
                    }
                    else if (gdHold > 0f)
                    {
                        if (gdHold >= s.gdCharge) GdFire(s);
                        gdHold = 0f;
                    }
                    if (gdAfter < 0f && DriveLeft <= 0f)
                    {
                        FinishDrive(s);
                        break;
                    }
                    GdNote();
                    break;
                }
                case PlanPhase.Recovery:
                    UpdateDriveRecovery(s, dt);
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        void GdNote()
        {
            planNote = (byte)(Mathf.Clamp(driveUses, 0, 15) | (gdHold > 0f ? GdCharging : 0) | (gdHold >= (PawnRushSkills != null ? PawnRushSkills.gdCharge : 0.5f) ? GdCharged : 0));
        }

        void GdFire(PawnRushSkillParams s)
        {
            int count = GdPath(GdMuzzle, planDir, s.gdLength, gdPoints);
            driveUses--;
            int shot = s.gdShots * 2 - driveUses;   // (a number that grows shot by shot, for the screens)
            float run = 0f;
            for (int i = 0; i + 1 < count; i++)
            {
                Fx(SkillFxKind.DriveLine, null, gdPoints[i], FlatDir(gdPoints[i + 1] - gdPoints[i]), i + (shot & 15) * 4, GdWidth(s, i), gdPoints[i + 1]);
                run += Flat(gdPoints[i + 1] - gdPoints[i]).magnitude;
            }
            gdHits.Clear();
            foreach (var e in All)
            {
                if (!DriveFoe(e)) continue;
                if (GdOnLine(s, gdPoints, count, e.bodies[0].position, out float along)) gdHits.Add((e, along));
            }
            gdHits.Sort((a, b) => a.along.CompareTo(b.along));
            Vector3 me = FeetPoint, ahead = me + planDir * s.gdKeep;
            foreach (var (e, along) in gdHits)
            {
                driveHit.Add(e);
                Vector3 to = GdBackOnLine(gdPoints, count, along, s.gdPull);
                // Never past the bishop: at least gdKeep in front of it on the first stretch.
                if (along - s.gdPull < s.gdKeep) to = ahead;
                var floor = DriveFloor(to, 3f, 12f) ?? e.FeetPoint;
                Vector3 hitAt = e.bodies[0].position;
                Fx(SkillFxKind.UltStrike, e, hitAt, FlatDir(to - hitAt), driveHit.Count, along);
                e.UltThrow(floor, s.gdPullTime, s.gdPullTop, s.gdKnock, this);
                e.LastSkillHit = $"롱 다이애거널: {along:0} m에서 꿰여 {Flat(floor - hitAt).magnitude:0} m 끌려옴";
                FeelHit(e, hitAt, planDir, FeelHard, driveHit.Count, true);
            }
            Kick(-planDir, s.gdRecoil, 0.25f, true);
            FeelHit(null, GdMuzzle, planDir, gdHits.Count > 0 ? FeelUltimate : FeelHard, Mathf.Max(1, gdHits.Count), false);
            Log($"롱 다이애거널: {run:0} m, 꺾임 {count - 2}, 꿰인 적 {gdHits.Count} (남은 {driveUses}발)");
            if (driveUses <= 0) gdAfter = 1f;
        }
    }
}
