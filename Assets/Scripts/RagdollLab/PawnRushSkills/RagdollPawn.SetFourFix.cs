using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The set 4판's round 1 under the movement rule (Docs/Skills/PAWN_RUSH_SET4_USER_SKILLS.md 9장, 11-26 night; the user
    /// on 2.1: "정교하게 레벨디자인된 3D 플랫폼 게임이기에 너무 이동을 도와줄법한 스킬은 있어선 안됨. 높게 점프해서 먼 거리를
    /// 이동하거나, 공중에서 돌진하는 등..", and on the planning session's table of the eleven it hits: "응, 넘겨"). The parts
    /// the eleven share:
    ///
    ///   돌아오기 (9.2)      땅속 헤엄, 더 루우욱! and 착석! carry the body far: where it was used a print stays (the piece's
    ///                      glyph in a ring, teal to its own side, red to the others), and once the skill's own part is over
    ///                      - or it is cut short, floored or caught - a band pulls taut for retBand s, then the body flies
    ///                      back onto the print in retFly s along a low arc, through everyone, untouchable, its keys off.
    ///                      A print whose floor has gone: the nearest floor round it (rings 0.6 m apart out to 3 m), else
    ///                      it stays where it is. A wall in the way: the last of it is a snap onto the print.
    ///   낭떠러지 앞 멈춤 (9.3) 직진! and 수평 룩 stop where the floor ends ahead: on one foot, arms going, "어, 어, 어!" for
    ///                      teeterTime s, pebbles falling. Only the skill's own movement stops: a push meanwhile still
    ///                      sends it over.
    ///   표적 없음 (9.4)      포크! and 엉덩방아 do not go without an enemy (SkipNoTarget4: no cooldown, the slot shakes, "삑").
    ///
    /// The host's; every PC draws it from S4Return and S4Teeter (PawnRushSkillFx.SetFourFix.cs). Not seen in Unity yet.
    /// </summary>
    public partial class RagdollPawn
    {
        const int Ret4None = 0, Ret4Marked = 1, Ret4Band = 2, Ret4Fly = 3;
        int ret4Stage, ret4Kind;
        Vector3 ret4Home, ret4Face, ret4From;
        float ret4T, ret4FlyFor;
        bool ret4Through;
        /// <summary>The skill under way stopped at an edge (its recovery is the teeter's time).</summary>
        bool teeter4;
        /// <summary>룩 리프트's hop aside: how long it still passes through everyone (RagdollPawn.SetFourC.cs).</summary>
        float liftHop4;

        /// <summary>On its way back to where its skill was used (9.2): no keys, untouchable, through everyone.</summary>
        public bool Returning4 => ret4Stage >= Ret4Band;

        // ---------------------------------------------------------------- 돌아오기 (9.2)

        /// <summary>Where the skill was used, kept for the way back (its first moment): the print on every screen.
        /// <paramref name="kind"/>: 1 땅속 헤엄, 2 더 루우욱!, 3 착석! (how each screen draws the band's start).</summary>
        void MarkHome4(int kind)
        {
            Vector3 home = FeetPoint;
            if (SolidRay(home + Vector3.up, Vector3.down, 3f, out var floor)) home = floor.point;
            CancelReturn4();
            ret4Home = home;
            ret4Face = FlatDir(facing);
            ret4Stage = Ret4Marked;
            ret4Kind = Mathf.Clamp(kind, 0, 9);
            Fx(SkillFxKind.S4Return, null, ret4Home, ret4Face, ret4Kind * 10, 0f);
        }

        /// <summary>The skill's own part is over, or cut short (EndPlanSkill): off home.</summary>
        void ReturnOnEnd4()
        {
            if (ret4Stage != Ret4Marked || NetworkPuppet) return;
            var s = PawnRushSkills;
            ret4Stage = Ret4Band;
            ret4T = 0f;
            ret4Home = SafeHome4(ret4Home);
            Vector3 me = bodies[0].position;
            Fx(SkillFxKind.S4Return, null, me, FlatDir(ret4Home - me), ret4Kind * 10 + 1, s != null ? s.retBand : 0.2f, ret4Home);
        }

        /// <summary>The print, or the nearest floor round it if its floor has gone; else where it is now.</summary>
        Vector3 SafeHome4(Vector3 home)
        {
            if (SolidRay(home + Vector3.up * 1.5f, Vector3.down, 3f, out var floor)) return floor.point;
            for (float r = 0.6f; r <= 3.01f; r += 0.6f)
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = home + Quaternion.AngleAxis(i * 45f, Vector3.up) * Vector3.forward * r;
                    if (SolidRay(p + Vector3.up * 1.5f, Vector3.down, 3f, out floor)) return floor.point;
                }
            return FeetPoint;
        }

        /// <summary>Every step (UpdateSetFourStates): the band pulled taut, then the flight onto the print.</summary>
        void StepReturn4(PawnRushSkillParams s, float dt)
        {
            if (liftHop4 > 0f)
            {
                liftHop4 -= dt;
                if (liftHop4 <= 0f && ret4Stage < Ret4Fly) PassThrough4(false);
            }
            if (ret4Stage < Ret4Band) return;
            ret4T += dt;
            GuardFromSkills(0.3f);
            if (ret4Stage == Ret4Band)
            {
                // Taut: the body leans a little toward the print.
                Vector3 to = FlatDir(ret4Home - bodies[0].position);
                AddVelocity(to * (s.retLean * dt / Mathf.Max(0.05f, s.retBand)));
                if (ret4T < s.retBand) return;
                ret4Stage = Ret4Fly;
                ret4T = 0f;
                ret4From = bodies[0].position;
                PassThrough4(true);
                Fx(SkillFxKind.S4Return, null, ret4From, FlatDir(ret4Home - ret4From), ret4Kind * 10 + 2, s.retFly, ret4Home);
                return;
            }
            // The flight: a low arc onto the print, through everyone and every hit.
            float T = Mathf.Max(0.1f, ret4FlyFor > 0f ? ret4FlyFor : s.retFly), k = Mathf.Clamp01(ret4T / T);
            Vector3 goal = ret4Home + Vector3.up * standHeight;
            float lift = s.retArc + 0.05f * Flat(goal - ret4From).magnitude;
            Vector3 p = Vector3.Lerp(ret4From, goal, k) + Vector3.up * (lift * 4f * k * (1f - k));
            Vector3 v = (goal - ret4From) / T + Vector3.up * (lift * 4f * (1f - 2f * k) / T);
            Fly(p, v, 0f, default, 40f);
            if (ret4T < T) return;
            EndFlight(Vector3.zero);
            if ((bodies[0].position - goal).magnitude > 0.9f) Teleport(goal, ret4Face);   // (a wall on the way)
            else Face(ret4Face);
            PassThrough4(false);
            HaltDash();
            ret4Stage = Ret4None;
            ret4FlyFor = 0f;
            Fx(SkillFxKind.S4Return, null, ret4Home, ret4Face, ret4Kind * 10 + 3);
        }

        /// <summary>Straight into the flight home (no band), over <paramref name="seconds"/>: the giant let go like a balloon.</summary>
        void FlyHomeNow4(float seconds)
        {
            if (ret4Stage != Ret4Marked || NetworkPuppet) return;
            ret4Home = SafeHome4(ret4Home);
            ret4Stage = Ret4Fly;
            ret4T = 0f;
            ret4FlyFor = Mathf.Max(0.1f, seconds);
            ret4From = bodies[0].position;
            PassThrough4(true);
            Fx(SkillFxKind.S4Return, null, ret4From, FlatDir(ret4Home - ret4From), ret4Kind * 10 + 2, ret4FlyFor, ret4Home);
        }

        /// <summary>A reset (a respawn, a piece switch): no way back, the print gone.</summary>
        void CancelReturn4()
        {
            if (liftHop4 > 0f)
            {
                liftHop4 = 0f;
                PassThrough4(false);
            }
            if (ret4Stage == Ret4None) return;
            if (ret4Stage == Ret4Fly) EndFlight();
            PassThrough4(false);
            ret4Stage = Ret4None;
            Fx(SkillFxKind.S4Return, null, ret4Home, ret4Face, ret4Kind * 10 + 4);
        }

        /// <summary>Another piece stands within <paramref name="radius"/> of <paramref name="p"/> (flat, about its height).</summary>
        bool PieceNear4(Vector3 p, float radius)
        {
            foreach (var o in All)
            {
                if (o == null || o == this) continue;
                Vector3 h = o.bodies[0].position;
                if (Flat(h - p).magnitude < radius && Mathf.Abs(h.y - p.y) < 1.8f) return true;
            }
            return false;
        }

        /// <summary>Through every other piece (and back).</summary>
        void PassThrough4(bool on)
        {
            if (ret4Through == on) return;
            ret4Through = on;
            foreach (var o in All)
                if (o != null && o != this) IgnorePiece(o, on);
        }

        // ---------------------------------------------------------------- 낭떠러지 앞 멈춤 (9.3)

        /// <summary>The floor ends <paramref name="ahead"/> m along <paramref name="way"/>: nothing within teeterDrop under
        /// that point.</summary>
        bool EdgeAhead4(Vector3 way, float ahead)
        {
            var s = PawnRushSkills;
            Vector3 p = FeetPoint + FlatDir(way) * ahead + Vector3.up * 0.6f;
            return !SolidRay(p, Vector3.down, 0.6f + (s != null ? s.teeterDrop : 1f), out _);
        }

        /// <summary>At the edge: the skill's movement along <paramref name="way"/> stops (nothing else), and on every screen
        /// it teeters on one foot.</summary>
        void Teeter4(Vector3 way)
        {
            var s = PawnRushSkills;
            way = FlatDir(way);
            HaltDash();
            Vector3 own = Flat(bodies[0].linearVelocity) - carryVel;
            float along = Vector3.Dot(own, way);
            if (along > 0f) AddVelocity(-way * along);
            teeter4 = true;
            Fx(SkillFxKind.S4Teeter, null, FeetPoint + way * 0.3f, way, 0, s != null ? s.teeterTime : 0.4f);
        }
    }
}
