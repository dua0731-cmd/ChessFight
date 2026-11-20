using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The marks of the Pawn Rush 2.0 skill plan prototypes (plan 1-나, RagdollPawn.SkillPlans.cs; JY-skill-lab, 2026-11-19):
    /// plain lines and rings, no effects - the square a queen's check names, the knight's two fork spots, the bishop's
    /// line and the rope of a pin, the rook's castling line, the ring under the piece a pawn may trip. Drawn every frame
    /// from each piece's public plan state, which a client gets from the host, so every PC shows the same. In the
    /// middle of the screen it tells the piece played here what is being done to it: a castling offered, a check, a pin.
    /// Added by the skill test bed and by the course's skills.
    /// </summary>
    [DefaultExecutionOrder(250)]
    public class PawnRushPlanMarks : MonoBehaviour
    {
        /// <summary>The pawn played at this PC.</summary>
        public Func<RagdollPawn> Viewer;

        static readonly Color Red = new Color(1f, 0.22f, 0.18f);
        static readonly Color Violet = new Color(0.74f, 0.52f, 1f);
        static readonly Color White = Color.white;
        static readonly Color Gold = new Color(1f, 0.82f, 0.3f);
        static readonly Color Grey = new Color(0.62f, 0.64f, 0.7f);

        readonly List<LineRenderer> pool = new List<LineRenderer>();
        int used;
        GUIStyle big, small;

        LineRenderer Next()
        {
            if (used == pool.Count) pool.Add(SkillMarks.Line(transform, $"Plan mark {pool.Count}"));
            return pool[used++];
        }

        static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, a);

        static Vector3 Chest(RagdollPawn p) => p.Hips.position + Vector3.up * 0.15f;

        void LateUpdate()
        {
            used = 0;
            var me = Viewer != null ? Viewer() : null;
            foreach (var pawn in RagdollPawn.All)
            {
                if (pawn == null || pawn.PawnRushSkills == null) continue;
                DrawPin(pawn);
                DrawWarded(pawn);
                DrawBookStates(pawn);
                DrawKitStates(pawn);
                // By the skill, not the piece: the loadout (the skill window) may put any skill on any piece.
                if (pawn.HasSkill(PawnRushSkillIds.EnPassant)) DrawPass(pawn, me);
                switch (pawn.PlanSkill)
                {
                    case PawnRushSkillIds.Fork: DrawFork(pawn); break;
                    case PawnRushSkillIds.Pin: DrawPinLine(pawn); break;
                    case PawnRushSkillIds.Castling:
                    case PawnRushSkillIds.CastleSwap: DrawCastle(pawn); break;
                    case PawnRushSkillIds.Check: DrawCheck(pawn); break;
                    // Queen of the Hill's (RagdollPawn.HillSkills.cs).
                    case PawnRushSkillIds.Ward: DrawWardCast(pawn); break;
                    case PawnRushSkillIds.Slash: DrawSlash(pawn); break;
                    case PawnRushSkillIds.AirVolley: DrawVolley(pawn); break;
                    case PawnRushSkillIds.PressLeap: DrawPressLeap(pawn); break;
                    // The skill idea book's bundle A (RagdollPawn.BookSkills.cs).
                    case PawnRushSkillIds.TouchMove: DrawTouch(pawn); break;
                    case PawnRushSkillIds.BackKick: DrawKick(pawn); break;
                    case PawnRushSkillIds.Ghost: DrawGhostCast(pawn); break;
                    case PawnRushSkillIds.BackPush: DrawPush(pawn); break;
                    case PawnRushSkillIds.TwoHares: DrawHares(pawn); break;
                    // The kits' basic row (RagdollPawn.KitSkills.cs).
                    case PawnRushSkillIds.BentLeap: DrawBentLeap(pawn); break;
                    case PawnRushSkillIds.Fianchetto: DrawFianchetto(pawn); break;
                    case PawnRushSkillIds.KingStep: DrawKingStep(pawn); break;
                    // The skill pool's plan 1 (RagdollPawn.PoolSkills.cs).
                    case PawnRushSkillIds.LongSnipe: DrawLongSnipe(pawn); break;
                }
            }
            for (int i = used; i < pool.Count; i++) SkillMarks.Hide(pool[i]);
        }

        /// <summary>A pin's rope, from the piece's waist to what it is tied to (drawn once for a tied pair), blinking as it
        /// runs out. The queen's elastic (the same rope with its own numbers) is gold.</summary>
        void DrawPin(RagdollPawn pawn)
        {
            if (!pawn.Pinned) return;
            var mate = pawn.PinMate;
            if (mate != null && mate.Pinned && mate.PinMate == pawn && pawn.GetInstanceID() > mate.GetInstanceID()) return;
            float a = pawn.PinLeft < 1f ? 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * 18f)) : 1f;
            Color c = pawn.Tied ? Gold : Violet;
            SkillMarks.Segment(Next(), pawn.LassoBelly, pawn.PinAnchor, Fade(c, a), 0.06f, false);
            SkillMarks.Circle(Next(), pawn.FeetPoint, 0.45f, Fade(c, a * 0.8f), 0.05f, 24);
            if (mate != null) SkillMarks.Circle(Next(), mate.FeetPoint, 0.45f, Fade(c, a * 0.8f), 0.05f, 24);
        }

        /// <summary>The pawn played here: a ring under the enemy that has just brushed past it (its key trips that one).</summary>
        void DrawPass(RagdollPawn pawn, RagdollPawn me)
        {
            if (pawn != me) return;
            var target = pawn.PassTarget;
            if (target == null) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 22f);
            SkillMarks.Circle(Next(), target.FeetPoint, 0.5f + 0.1f * pulse, Fade(SkillMarks.Orange, 0.6f + 0.4f * pulse), 0.09f, 24);
        }

        /// <summary>The knight's fork: where it comes down, and the two spots its hind legs kick.</summary>
        void DrawFork(RagdollPawn pawn)
        {
            var phase = pawn.PlanPhaseNow;
            if (phase != PlanPhase.Windup && phase != PlanPhase.Active) return;
            var s = pawn.PawnRushSkills;
            Color spot = Fade(SkillMarks.Target, phase == PlanPhase.Active ? 1f : 0.75f);
            SkillMarks.Circle(Next(), pawn.PlanPoint, 0.35f, Fade(White, 0.8f), 0.05f, 20);
            for (int side = 0; side < 2; side++)
            {
                Vector3 at = pawn.ForkSpot(side);
                SkillMarks.Circle(Next(), at, s.forkRadius, spot, 0.07f, 32);
                SkillMarks.Segment(Next(), pawn.PlanPoint + Vector3.up * 0.05f, at + Vector3.up * 0.05f, Fade(SkillMarks.Target, 0.5f), 0.04f);
            }
        }

        /// <summary>The bishop's pin: aiming, the line as far as it reaches (to the enemy it would take first); shot, the
        /// line it took for a moment.</summary>
        void DrawPinLine(RagdollPawn pawn)
        {
            var phase = pawn.PlanPhaseNow;
            var s = pawn.PawnRushSkills;
            Vector3 hands = pawn.LassoHands;
            if (phase == PlanPhase.Aim)
            {
                Vector3 end = hands + pawn.PlanDir * s.pinRange;
                SkillMarks.Segment(Next(), hands, end, Fade(Violet, 0.45f), 0.04f, false);
                var target = pawn.PlanTarget;
                if (target != null) SkillMarks.Circle(Next(), target.FeetPoint, 0.5f, Fade(Violet, 0.9f), 0.06f, 24);
            }
            else if (phase == PlanPhase.Recovery)
            {
                float a = 1f - Mathf.Clamp01(pawn.PlanTime / Mathf.Max(0.05f, s.pinRecovery));
                SkillMarks.Segment(Next(), hands, pawn.PlanPoint, Fade(White, 0.4f + 0.6f * a), 0.09f, false);
            }
        }

        /// <summary>The rook's castling: a line to the piece it would swap with or is about to, and a ring under each.</summary>
        void DrawCastle(RagdollPawn pawn)
        {
            var phase = pawn.PlanPhaseNow;
            if (phase != PlanPhase.Aim && phase != PlanPhase.Windup) return;
            var mate = pawn.PlanTarget;
            var s = pawn.PawnRushSkills;
            if (mate == null)
            {
                if (phase == PlanPhase.Aim) SkillMarks.Circle(Next(), pawn.FeetPoint, 0.5f, Fade(White, 0.35f), 0.04f, 24);
                return;
            }
            bool ally = mate.Team == pawn.Team && pawn.Team != Teams.None;
            Color c = ally ? SkillMarks.Cyan : SkillMarks.Orange;
            float a = phase == PlanPhase.Aim ? 0.5f : 1f;
            SkillMarks.Segment(Next(), Chest(pawn), Chest(mate), Fade(c, a), phase == PlanPhase.Aim ? 0.04f : 0.07f, false);
            SkillMarks.Circle(Next(), pawn.FeetPoint, 0.5f, Fade(c, a), 0.06f, 24);
            SkillMarks.Circle(Next(), mate.FeetPoint, 0.5f, Fade(c, a), 0.06f, 24);
            if (phase != PlanPhase.Windup) return;
            // The time left: the teammate's to answer in, or the enemy's warning, as a ring closing on the other piece.
            float total = ally ? pawn.CastleWindow : s.castleEnemyWindup;
            float left = 1f - Mathf.Clamp01(pawn.PlanTime / Mathf.Max(0.05f, total));
            SkillMarks.Circle(Next(), mate.FeetPoint, 0.5f + 0.9f * left, Fade(c, 0.9f), 0.04f, 28);
        }

        /// <summary>The queen's check: aiming, a ring under the enemy she would name; named, its square in red with a ring
        /// closing on it as the time runs out, and the line from her (a piece standing on that line takes the blow).</summary>
        void DrawCheck(RagdollPawn pawn)
        {
            var phase = pawn.PlanPhaseNow;
            var s = pawn.PawnRushSkills;
            var target = pawn.PlanTarget;
            if (phase == PlanPhase.Aim)
            {
                if (target == null) return;
                float blink = 0.55f + 0.45f * Mathf.Sin(Time.time * 14f);
                SkillMarks.Circle(Next(), target.FeetPoint, 0.55f, Fade(Red, blink), 0.06f, 24);
                SkillMarks.Segment(Next(), Chest(pawn), Chest(target), Fade(Red, 0.3f), 0.03f, false);
                return;
            }
            if (phase != PlanPhase.Active) return;
            float left = 1f - Mathf.Clamp01(pawn.PlanTime / Mathf.Max(0.05f, s.checkSeconds));
            Vector3 square = pawn.PlanPoint;
            SkillMarks.Circle(Next(), square, s.checkSquare, Red, 0.12f, 40);
            SkillMarks.Circle(Next(), square, Mathf.Max(0.1f, s.checkSquare * left), Fade(Red, 0.85f), 0.06f, 32);
            SkillMarks.Circle(Next(), square, s.checkSquare * 0.5f, Fade(Red, 0.35f), 0.05f, 28);
            if (target != null) SkillMarks.Segment(Next(), Chest(pawn), Chest(target), Fade(Red, 0.6f), 0.05f, false);
        }

        // ---------------------------------------------------------------- Queen of the Hill's six (RagdollPawn.HillSkills.cs)

        /// <summary>A close guard on a piece: a gold ring under it, blinking as it runs out.</summary>
        void DrawWarded(RagdollPawn pawn)
        {
            if (!pawn.Warded) return;
            float a = pawn.WardLeft < 0.6f ? 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(Time.time * 18f)) : 1f;
            SkillMarks.Circle(Next(), pawn.FeetPoint, 0.55f, Fade(Gold, a), 0.08f, 24);
            SkillMarks.Circle(Next(), pawn.FeetPoint, 0.42f, Fade(Gold, a * 0.5f), 0.04f, 20);
        }

        /// <summary>The close guard being given: its reach, and a ring growing out to it over the windup.</summary>
        void DrawWardCast(RagdollPawn pawn)
        {
            var phase = pawn.PlanPhaseNow;
            var s = pawn.PawnRushSkills;
            Vector3 at = pawn.FeetPoint;
            if (phase == PlanPhase.Windup)
            {
                float k = Mathf.Clamp01(pawn.PlanTime / Mathf.Max(0.05f, s.wardWindup));
                SkillMarks.Circle(Next(), at, s.wardRadius, Fade(Gold, 0.45f + 0.4f * k), 0.06f, 40);
                SkillMarks.Circle(Next(), at, Mathf.Max(0.2f, s.wardRadius * k), Fade(Gold, 0.9f), 0.05f, 32);
            }
            else if (phase == PlanPhase.Recovery)
            {
                float a = 1f - Mathf.Clamp01(pawn.PlanTime / Mathf.Max(0.05f, s.wardRecovery));
                SkillMarks.Circle(Next(), at, s.wardRadius, Fade(Gold, a), 0.1f, 40);
            }
        }

        /// <summary>The long slash: its strip along the floor to where it ends (a wall, a guarded piece), brighter once it
        /// is locked, and its front running out along it.</summary>
        void DrawSlash(RagdollPawn pawn)
        {
            var phase = pawn.PlanPhaseNow;
            if (phase == PlanPhase.Recovery || phase == PlanPhase.None) return;
            var s = pawn.PawnRushSkills;
            Vector3 from = pawn.FeetPoint + Vector3.up * 0.05f, to = pawn.PlanPoint + Vector3.up * 0.05f;
            Vector3 dir = pawn.PlanDir, side = Vector3.Cross(Vector3.up, dir) * (s.slashWidth * 0.5f);
            float a = phase == PlanPhase.Aim ? 0.4f : phase == PlanPhase.Windup ? 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 20f)) : 1f;
            Color c = Fade(Red, a);
            SkillMarks.Segment(Next(), from + side, to + side, c, 0.05f);
            SkillMarks.Segment(Next(), from - side, to - side, c, 0.05f);
            SkillMarks.Segment(Next(), to + side, to - side, c, 0.05f);
            if (phase != PlanPhase.Active) return;
            float k = Mathf.Clamp01(pawn.PlanTime / Mathf.Max(0.01f, s.slashTravel));
            Vector3 front = Vector3.Lerp(from, to, k);
            SkillMarks.Segment(Next(), front + side, front - side, White, 0.14f);
        }

        /// <summary>The air barrage: the X the shot will make where it is aimed (grey where it cannot go), and the shot on
        /// its way.</summary>
        void DrawVolley(RagdollPawn pawn)
        {
            var phase = pawn.PlanPhaseNow;
            if (phase != PlanPhase.Aim && phase != PlanPhase.Active) return;
            var s = pawn.PawnRushSkills;
            bool flying = phase == PlanPhase.Active && pawn.PlanTime < s.volleyFlight;
            if (phase == PlanPhase.Active && !flying) return;
            Vector3 c = pawn.PlanPoint + Vector3.up * 0.05f;
            Color col = pawn.PlanAimValid ? Fade(Violet, flying ? 1f : 0.6f) : Fade(Grey, 0.5f);
            for (int i = 0; i < 2; i++)
            {
                Vector3 arm = Quaternion.Euler(0f, i == 0 ? 45f : -45f, 0f) * pawn.PlanDir * s.volleyArm;
                SkillMarks.Segment(Next(), c - arm, c + arm, col, flying ? 0.1f : 0.06f);
            }
            SkillMarks.Circle(Next(), pawn.PlanPoint, s.volleyCenter, col, 0.05f, 28);
            if (!flying) return;
            float k = Mathf.Clamp01(pawn.PlanTime / Mathf.Max(0.05f, s.volleyFlight));
            Vector3 shot = Vector3.Lerp(Chest(pawn), pawn.PlanPoint, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 1.2f);
            SkillMarks.Circle(Next(), shot, 0.16f, White, 0.1f, 12);
        }

        /// <summary>The press leap: the landing circle where it is aimed (grey where it cannot go), a ring on the enemy whose
        /// head it would come down on, and the way there once it goes.</summary>
        void DrawPressLeap(RagdollPawn pawn)
        {
            var phase = pawn.PlanPhaseNow;
            if (phase == PlanPhase.Recovery || phase == PlanPhase.None) return;
            var s = pawn.PawnRushSkills;
            bool aim = phase == PlanPhase.Aim;
            Color col = !aim || pawn.PlanAimValid ? Fade(SkillMarks.Target, aim ? 0.6f : 1f) : Fade(Grey, 0.5f);
            SkillMarks.Circle(Next(), pawn.PlanPoint, s.pressRadius, col, aim ? 0.06f : 0.1f, 36);
            var target = pawn.PlanTarget;
            if (target != null) SkillMarks.Circle(Next(), target.FeetPoint, 0.5f, Fade(Red, 0.6f + 0.4f * Mathf.Sin(Time.time * 16f)), 0.07f, 24);
            if (!aim) SkillMarks.Segment(Next(), pawn.FeetPoint + Vector3.up * 0.05f, pawn.PlanPoint + Vector3.up * 0.05f, Fade(SkillMarks.Target, 0.5f), 0.04f);
        }

        // ---------------------------------------------------------------- the skill idea book's bundle A (RagdollPawn.BookSkills.cs)

        /// <summary>What those skills leave on a piece: an orange ring beating under one that cannot stand still, a small
        /// grey one under numb hands, white rings round a ghost (blinking as it runs out).</summary>
        void DrawBookStates(RagdollPawn pawn)
        {
            Vector3 feet = pawn.FeetPoint;
            if (pawn.Touched)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 16f);
                SkillMarks.Circle(Next(), feet, 0.42f + 0.08f * pulse, Fade(SkillMarks.Orange, 0.7f + 0.3f * pulse), 0.08f, 24);
            }
            if (pawn.Numb) SkillMarks.Circle(Next(), feet, 0.34f, Fade(Grey, 0.9f), 0.07f, 20);
            if (pawn.Ghosted)
            {
                float a = pawn.GhostLeft < 1f ? 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.time * 18f)) : 1f;
                SkillMarks.Circle(Next(), feet, 0.6f, Fade(White, a), 0.06f, 28);
                SkillMarks.Circle(Next(), feet, 0.72f, Fade(White, a * 0.5f), 0.04f, 28);
                SkillMarks.Circle(Next(), Chest(pawn), 0.38f, Fade(White, a * 0.6f), 0.04f, 20);
            }
        }

        /// <summary>The pawn's palm: its reach along the aim, and a ring under the piece it would touch.</summary>
        void DrawTouch(RagdollPawn pawn)
        {
            var phase = pawn.PlanPhaseNow;
            if (phase != PlanPhase.Windup && phase != PlanPhase.Recovery) return;
            float a = phase == PlanPhase.Windup ? 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 24f)) : 0.5f;
            SkillMarks.Segment(Next(), Chest(pawn), pawn.PlanPoint, Fade(SkillMarks.Orange, a), 0.07f, false);
            SkillMarks.Circle(Next(), pawn.PlanPoint, 0.14f, Fade(White, a), 0.06f, 12);
            var target = pawn.PlanTarget;
            if (phase == PlanPhase.Windup && target != null)
                SkillMarks.Circle(Next(), target.FeetPoint, 0.45f, Fade(SkillMarks.Orange, 0.9f), 0.06f, 24);
        }

        /// <summary>The knight's back kick: the strip behind it that the hind legs sweep, blinking through the warning.</summary>
        void DrawKick(RagdollPawn pawn)
        {
            var phase = pawn.PlanPhaseNow;
            if (phase != PlanPhase.Windup && phase != PlanPhase.Recovery) return;
            var s = pawn.PawnRushSkills;
            Vector3 dir = pawn.PlanDir, from = pawn.FeetPoint + Vector3.up * 0.05f, to = from + dir * s.kickReach;
            Vector3 side = Vector3.Cross(Vector3.up, dir) * (s.kickWidth * 0.5f);
            float a = phase == PlanPhase.Windup
                ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 22f))
                : 1f - Mathf.Clamp01(pawn.PlanTime / Mathf.Max(0.05f, s.kickRecovery));
            Color c = Fade(SkillMarks.Target, a);
            SkillMarks.Segment(Next(), from + side, to + side, c, 0.05f);
            SkillMarks.Segment(Next(), from - side, to - side, c, 0.05f);
            SkillMarks.Segment(Next(), to + side, to - side, c, 0.05f);
        }

        /// <summary>The bishop changing colour: a ring closing on it through the short warning.</summary>
        void DrawGhostCast(RagdollPawn pawn)
        {
            if (pawn.PlanPhaseNow != PlanPhase.Windup) return;
            float k = 1f - Mathf.Clamp01(pawn.PlanTime / Mathf.Max(0.05f, pawn.PawnRushSkills.ghostWindup));
            SkillMarks.Circle(Next(), pawn.FeetPoint, 0.6f + 0.8f * k, Fade(White, 0.9f), 0.05f, 28);
        }

        /// <summary>The rook's push: the line from its hands through the piece it will send to where that one comes down,
        /// and a ring closing on the piece as the warning runs out (cyan for a teammate, orange for an enemy).</summary>
        void DrawPush(RagdollPawn pawn)
        {
            if (pawn.PlanPhaseNow != PlanPhase.Windup) return;
            var t = pawn.PlanTarget;
            if (t == null) return;
            var s = pawn.PawnRushSkills;
            bool ally = t.Team == pawn.Team && pawn.Team != Teams.None;
            Color c = ally ? SkillMarks.Cyan : SkillMarks.Orange;
            SkillMarks.Segment(Next(), Chest(pawn), Chest(t), Fade(c, 0.9f), 0.07f, false);
            SkillMarks.Segment(Next(), t.FeetPoint + Vector3.up * 0.05f, pawn.PlanPoint + Vector3.up * 0.05f, Fade(c, 0.7f), 0.06f);
            SkillMarks.Circle(Next(), pawn.PlanPoint, 0.5f, Fade(c, 0.9f), 0.07f, 24);
            float left = 1f - Mathf.Clamp01(pawn.PlanTime / Mathf.Max(0.05f, s.pushWindup));
            SkillMarks.Circle(Next(), t.FeetPoint, 0.45f + 0.8f * left, Fade(c, 0.9f), 0.05f, 28);
        }

        /// <summary>The queen's two hares: a line from her hands to each of the two she reaches for (the second's waist is
        /// the plan's point), brighter as the elastic is about to go on.</summary>
        void DrawHares(RagdollPawn pawn)
        {
            if (pawn.PlanPhaseNow != PlanPhase.Windup) return;
            var s = pawn.PawnRushSkills;
            float a = 0.5f + 0.5f * Mathf.Clamp01(pawn.PlanTime / Mathf.Max(0.05f, s.hareWindup));
            Vector3 hands = pawn.LassoHands;
            var first = pawn.PlanTarget;
            if (first != null)
            {
                SkillMarks.Segment(Next(), hands, first.LassoBelly, Fade(Gold, a), 0.05f, false);
                SkillMarks.Circle(Next(), first.FeetPoint, 0.45f, Fade(Gold, a), 0.06f, 24);
            }
            SkillMarks.Segment(Next(), hands, pawn.PlanPoint, Fade(Gold, a), 0.05f, false);
            SkillMarks.Circle(Next(), pawn.PlanPoint, 0.3f, Fade(Gold, a), 0.06f, 16);
        }

        // ---------------------------------------------------------------- the kits' basic row (RagdollPawn.KitSkills.cs)

        static readonly Color Ice = new Color(0.55f, 0.8f, 1f);

        /// <summary>Heavy feet (a bishop's fianchetto took the piece): a pale blue ring beating slowly under it, blinking
        /// as it runs out.</summary>
        void DrawKitStates(RagdollPawn pawn)
        {
            if (!pawn.Slowed) return;
            float a = pawn.SlowLeft < 0.6f ? 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(Time.time * 18f)) : 1f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
            Vector3 feet = pawn.FeetPoint;
            SkillMarks.Circle(Next(), feet, 0.4f + 0.05f * pulse, Fade(Ice, a), 0.08f, 24);
            SkillMarks.Circle(Next(), feet, 0.26f, Fade(Ice, a * 0.6f), 0.05f, 20);
        }

        /// <summary>The knight's bent leap: where it comes down (worked out when it takes off, turns or bounces).</summary>
        void DrawBentLeap(RagdollPawn pawn)
        {
            var phase = pawn.PlanPhaseNow;
            if (phase != PlanPhase.Windup && phase != PlanPhase.Active) return;
            bool air = phase == PlanPhase.Active;
            if (pawn.LeapBlunder)
            {
                // The pool's N-기1: it will come down on nothing - a red ring with a cross in it.
                Vector3 c = pawn.PlanPoint;
                float blink = 0.6f + 0.4f * Mathf.Sin(Time.time * 18f);
                SkillMarks.Circle(Next(), c, 0.5f, Fade(Red, blink), 0.08f, 28);
                SkillMarks.Segment(Next(), c + new Vector3(-0.35f, 0f, -0.35f), c + new Vector3(0.35f, 0f, 0.35f), Fade(Red, blink), 0.07f, false);
                SkillMarks.Segment(Next(), c + new Vector3(-0.35f, 0f, 0.35f), c + new Vector3(0.35f, 0f, -0.35f), Fade(Red, blink), 0.07f, false);
                return;
            }
            SkillMarks.Circle(Next(), pawn.PlanPoint, 0.5f, Fade(SkillMarks.Target, air ? 0.95f : 0.6f), air ? 0.08f : 0.05f, 28);
            if (air) SkillMarks.Circle(Next(), pawn.PlanPoint, 0.2f, Fade(White, 0.8f), 0.05f, 16);
        }

        /// <summary>The pool's long diagonal snipe (B-중7): kneeling, the line as far as it reaches; drawing, the line grows and
        /// thins (violet to gold), and from snipeWarnAfter a light blinks at the target's feet; shot, the line it took for a
        /// moment (gold for a "최선수").</summary>
        void DrawLongSnipe(RagdollPawn pawn)
        {
            var phase = pawn.PlanPhaseNow;
            var s = pawn.PawnRushSkills;
            Vector3 hands = pawn.LassoHands;
            var target = pawn.PlanTarget;
            if (phase == PlanPhase.Aim || phase == PlanPhase.Windup)
            {
                bool drawing = phase == PlanPhase.Windup;
                float full = drawing ? Mathf.Clamp01(pawn.PlanTime / Mathf.Max(0.05f, s.snipeCharge)) : 0f;
                Color line = Color.Lerp(Violet, Gold, full);
                SkillMarks.Segment(Next(), hands, pawn.PlanPoint, Fade(line, drawing ? 0.6f + 0.35f * full : 0.4f), Mathf.Lerp(0.07f, 0.02f, full), false);
                SkillMarks.Circle(Next(), pawn.FeetPoint, 0.55f, Fade(line, 0.8f), 0.06f, 28);
                if (target == null) return;
                SkillMarks.Circle(Next(), target.FeetPoint, 0.5f, Fade(line, 0.9f), 0.06f, 24);
                if (drawing && pawn.PlanTime >= s.snipeWarnAfter)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 16f);
                    SkillMarks.Circle(Next(), target.FeetPoint, 0.16f + 0.1f * pulse, Fade(White, 0.95f), 0.12f, 16);
                }
            }
            else if (phase == PlanPhase.Recovery)
            {
                float a = 1f - Mathf.Clamp01(pawn.PlanTime / 0.35f);
                if (a > 0f) SkillMarks.Segment(Next(), hands, pawn.PlanPoint, Fade(pawn.SnipeBest ? Gold : White, 0.3f + 0.7f * a), 0.12f, false);
            }
        }

        /// <summary>The bishop's fianchetto: seated, its line as far as it reaches now (it grows while it aims) with a tick
        /// where it will end once fully aimed, and a ring under the enemy it would take; shot, the line it took for a
        /// moment.</summary>
        void DrawFianchetto(RagdollPawn pawn)
        {
            var phase = pawn.PlanPhaseNow;
            var s = pawn.PawnRushSkills;
            Vector3 hands = pawn.LassoHands;
            if (phase == PlanPhase.Aim)
            {
                float full = Mathf.Clamp01(pawn.PlanTime / Mathf.Max(0.05f, s.fianCharge));
                Color line = Color.Lerp(Violet, Gold, full);
                SkillMarks.Segment(Next(), hands, pawn.PlanPoint, Fade(line, 0.55f + 0.3f * full), 0.04f + 0.03f * full, false);
                SkillMarks.Circle(Next(), pawn.FeetPoint, 0.55f, Fade(line, 0.8f), 0.06f, 28);
                var target = pawn.PlanTarget;
                if (target != null) SkillMarks.Circle(Next(), target.FeetPoint, 0.5f, Fade(line, 0.95f), 0.07f, 24);
            }
            else if (phase == PlanPhase.Recovery)
            {
                float a = 1f - Mathf.Clamp01(pawn.PlanTime / Mathf.Max(0.05f, s.fianRecovery));
                SkillMarks.Segment(Next(), hands, pawn.PlanPoint, Fade(White, 0.4f + 0.6f * a), 0.1f, false);
            }
        }

        /// <summary>The king's step: the square he steps to, for the moment it takes.</summary>
        void DrawKingStep(RagdollPawn pawn)
        {
            var phase = pawn.PlanPhaseNow;
            if (phase != PlanPhase.Windup && phase != PlanPhase.Active) return;
            SkillMarks.Circle(Next(), pawn.PlanPoint, 0.45f, Fade(Gold, 0.85f), 0.06f, 24);
        }

        // ---------------------------------------------------------------- what is being done to the piece played here

        void OnGUI()
        {
            var me = Viewer != null ? Viewer() : null;
            if (me == null || me.PawnRushSkills == null) return;
            string line = null, sub = null;
            float share = 0f;
            var rook = me.CastleOfferingMe();
            var queen = me.CheckedBy();
            var pusher = me.PushedBy();
            var sniper = me.SnipedBy();
            if (rook != null && rook.PawnRushSkills != null)
            {
                float window = Mathf.Max(0.1f, rook.CastleWindow);
                line = "캐슬링!  스킬 키로 받기";
                sub = $"{rook.DisplayName}와 자리 바꾸기 · {rook.CastleOfferLeft:0.0}초";
                share = Mathf.Clamp01(rook.CastleOfferLeft / window);
            }
            else if (pusher != null && pusher.PawnRushSkills != null)
            {
                bool mine = pusher.Team == me.Team && me.Team != Teams.None;
                line = mine ? "뒤에서 밀기!  받기 싫으면 점프" : "룩이 민다!  점프로 빠져요";
                sub = $"{pusher.DisplayName} · {pusher.PushLeft:0.0}초";
                share = Mathf.Clamp01(pusher.PushLeft / Mathf.Max(0.1f, pusher.PawnRushSkills.pushWindup));
            }
            else if (queen != null && queen.PawnRushSkills != null)
            {
                float window = Mathf.Max(0.1f, queen.PawnRushSkills.checkSeconds);
                line = "체크!  빨간 칸 밖으로";
                sub = $"{queen.DisplayName} · {queen.CheckLeft:0.0}초";
                share = Mathf.Clamp01(queen.CheckLeft / window);
            }
            else if (sniper != null && sniper.PawnRushSkills != null)
            {
                // The pool's long diagonal snipe: the light at my feet.
                line = "저격!  비숍이 노려요";
                sub = $"{sniper.DisplayName} · 갈지자로, 기둥 뒤로, 비숍에게 붙어요";
                share = Mathf.Clamp01(sniper.PlanTime / Mathf.Max(0.05f, sniper.PawnRushSkills.snipeCharge));
            }
            else if (me.Touched && me.PawnRushSkills != null)
            {
                line = "터치 무브!  멈출 수 없어요";
                sub = $"방향은 틀 수 있어요 · {me.TouchLeft:0.0}초";
                share = Mathf.Clamp01(me.TouchLeft / Mathf.Max(0.1f, me.PawnRushSkills.touchSeconds));
            }
            else if (me.Tied && me.PawnRushSkills != null)
            {
                var mate = me.PinMate;
                line = "두 마리 토끼!  묶였어요";
                sub = (mate != null ? $"{mate.DisplayName}와 " : "") + $"고무줄 {me.PawnRushSkills.hareRest:0.#} m · {me.PinLeft:0.0}초";
                share = Mathf.Clamp01(me.PinLeft / Mathf.Max(0.1f, me.PawnRushSkills.hareSeconds));
            }
            else if (me.Pinned && me.PawnRushSkills != null)
            {
                var mate = me.PinMate;
                line = "핀!  묶였어요";
                sub = (mate != null ? $"{mate.DisplayName}와 한 줄 · " : "벽에 묶임 · ") + $"{me.PinLeft:0.0}초";
                share = Mathf.Clamp01(me.PinLeft / Mathf.Max(0.1f, me.PawnRushSkills.pinSeconds));
            }
            else if (me.Numb && me.PawnRushSkills != null)
            {
                line = "손 저림!  아무것도 못 잡아요";
                sub = $"{me.NumbLeft:0.0}초";
                share = Mathf.Clamp01(me.NumbLeft / Mathf.Max(0.1f, me.PawnRushSkills.kickNumb));
            }
            else if (me.Ghosted && me.PawnRushSkills != null)
            {
                line = "반대색!  적이 통과해요";
                sub = $"발판 인원에는 안 들어가요 · {me.GhostLeft:0.0}초";
                share = Mathf.Clamp01(me.GhostLeft / Mathf.Max(0.1f, me.PawnRushSkills.ghostSeconds));
            }
            else if (me.Slowed && me.PawnRushSkills != null)
            {
                line = "피앙케토!  발이 무거워요";
                sub = $"전력질주가 안 돼요 · {me.SlowLeft:0.0}초";
                share = Mathf.Clamp01(me.SlowLeft / Mathf.Max(0.1f, me.PawnRushSkills.fianSlowSeconds));
            }
            else if (me.Warded && me.PawnRushSkills != null)
            {
                line = "호위!  넘어지지 않아요";
                sub = $"밀림도 조금만 · {me.WardLeft:0.0}초";
                share = Mathf.Clamp01(me.WardLeft / Mathf.Max(0.1f, me.PawnRushSkills.wardTime));
            }
            // The skill pool's plan 1: what my own skill just did (RagdollPawn.PoolSkills.cs).
            else if (me.LeapBlunder)
            {
                line = "블런더?!";
                sub = "내려설 곳이 없어요";
                share = 1f;
            }
            else if (me.SnipeBest)
            {
                line = "최선수!";
                sub = "멀리서 맞혀 넘어뜨렸어요";
                share = 1f - Mathf.Clamp01(me.PlanTime / 0.7f);
            }
            else if (me.CheckAgainLeft > 0f && me.PawnRushSkills != null)
            {
                line = "체크를 이어요!";
                sub = $"비켜 나갔어요 · 스킬 키로 한 번 더 · {me.CheckAgainLeft:0.0}초";
                share = Mathf.Clamp01(me.CheckAgainLeft / Mathf.Max(0.1f, me.PawnRushSkills.checkAgainWindow));
            }
            if (line == null) return;
            if (big == null)
            {
                var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI" }, 32);
                big = new GUIStyle(GUI.skin.label) { font = font, fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                big.normal.textColor = new Color(0.98f, 0.96f, 0.9f);
                small = new GUIStyle(big) { fontSize = 17, fontStyle = FontStyle.Normal };
                small.normal.textColor = new Color(0.86f, 0.88f, 0.92f);
            }
            var white = Texture2D.whiteTexture;
            GUI.depth = -10;
            float w = 380f, h = 96f, x = (Screen.width - w) * 0.5f, y = Screen.height * 0.62f;
            GUI.color = new Color(0.05f, 0.08f, 0.16f, 0.78f);
            GUI.DrawTexture(new Rect(x, y, w, h), white);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y + 8f, w, 40f), line, big);
            GUI.Label(new Rect(x, y + 46f, w, 24f), sub, small);
            GUI.color = new Color(0.35f, 0.38f, 0.45f, 1f);
            GUI.DrawTexture(new Rect(x + 20f, y + h - 18f, w - 40f, 6f), white);
            GUI.color = new Color(0.86f, 0.9f, 0.96f, 1f);
            GUI.DrawTexture(new Rect(x + 20f, y + h - 18f, (w - 40f) * share, 6f), white);
            GUI.color = Color.white;
        }
    }
}
