using ChessFight.Network;
using ChessFight.VFX;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The knight's F and E effects over his realistic drawing (R179, 10-28, 사용자: "나이트 작업 시작하자", then "적 칸은
    /// 빨강으로 하고 F5 만들어"), made layer by layer by the editor menu ChessFight/VFX/Knight/Build
    /// (Assets/Scripts/VFXEditor/KnightVfxBuilder.cs) into Assets/VFX/Prefabs/Knight, listed in Resources/VfxSets/Knight.
    /// Until the menu has been run there is no set and nothing plays. So far: the fork drawn out from the square it lands
    /// on (F_Fork, on SfFxKind.KnightLand), on the squares the dive aimed at and red where an enemy stands; the crescent
    /// its hind hooves draw over the somersault of its reversal (E_Flip, on SfFxKind.KnightFlip, R182); the star where
    /// they strike whoever its buck takes, with a few long streaks the way it is kicked (E_Kick, on
    /// SfFxKind.KnightKickHit, R189); the flash where they kick off the edge into it, with a few streaks fanned down
    /// over the drop (E_Spring, on SfFxKind.KnightFlip too, R192); the light drawn in to them as it crouches before it
    /// (E_Coil, on SfFxKind.KnightCrouch, R195). Every screen draws them (they hang off the same moments and the same hooves).
    /// <see cref="KnightVfxOn"/> false leaves the realistic drawing alone.
    /// </summary>
    public partial class SwordFightSkillFx
    {
        public static bool KnightVfxOn = true;

        /// <summary>F5, the fork from where it lands (e.at, under it; e.dir its way; e.size a square): the squares the dive
        /// aimed at (SwordFightSkills.KnightSquares, none where there is no floor), red where an enemy stands on one, the
        /// plate red if one is stamped on it.</summary>
        void KnightVfxFork(SfFxEvent e)
        {
            var s = e.by;
            if (!KnightVfxOn || s == null || s.Pawn == null) return;
            var go = PieceVfx("Knight", "F_Fork", Kit.FloorUnder(e.at + Vector3.up * 0.5f, 1f), Kit.FlatDir(e.dir, Vector3.forward));
            var fork = go != null ? go.GetComponentInChildren<VfxForkGrid>() : null;
            if (fork == null) return;
            var S = this.S;
            int n = SwordFightSkills.KnightMoves.Length;
            var on = new bool[n];
            var foe = new bool[n];
            for (int i = 0; i < n; i++)
            {
                on[i] = s.KnightSquareOn[i];
                foe[i] = on[i] && EnemyOn(s, s.KnightSquares[i], S != null ? S.knightSquare + 0.15f : 0.8f);
            }
            fork.Configure(e.size > 0.1f ? e.size : 1.25f, on, foe, EnemyOn(s, e.at, S != null ? S.knightStomp : 0.85f));
        }

        /// <summary>E1, the crouch (e.at is where it will land, not it; e.dir its way there): the threads drawn in to the
        /// floor between its hind hooves and the glow filling there, on the floor's top under them (held in the air off
        /// the edge, at the hooves themselves). Broken off - struck, or its enemy backing away - it is cleared at once;
        /// as it springs it has died away on its own and E2 flashes there.</summary>
        void KnightVfxCoil(SfFxEvent e)
        {
            var s = e.by;
            if (!KnightVfxOn || s == null || s.Pawn == null) return;
            Vector3 feet = Hooves(s.Pawn);
            var floor = SwordFightEdgeFloor.Current;
            Vector3 at = floor != null && floor.OnFloor(feet) ? new Vector3(feet.x, floor.Top, feet.z) : feet;
            var go = PieceVfx("Knight", "E_Coil", at, Kit.FlatDir(e.dir, Vector3.forward));
            if (go == null) return;
            var systems = go.GetComponentsInChildren<ParticleSystem>();
            kit.Run(1f, (fx, d) =>
            {
                if (go == null || s == null) return false;
                if (s.Piece == PieceKind.Knight && s.EdgeStage == SfEdge.Windup) return true;
                if (s.EdgeStage == SfEdge.Active || s.EdgeStage == SfEdge.Recovery) return false;
                // Another screen may hear of the crouch a moment before its stage turns: wait a little for it.
                if (fx.age < 0.2f) return true;
                foreach (var ps in systems) if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return false;
            });
        }

        /// <summary>How far in from the lip its hooves may be for E2 to flash on the brass (beyond, where they leave the board).</summary>
        const float SpringLip = 0.6f;

        /// <summary>E2, as it springs (e.at the floor under it, e.dir its way to where it lands): the flash where its hind
        /// hooves kick off. Near the edge that is the brass lip, nearest them, the streaks fanned out and down over the
        /// drop; further in it is where they leave the board, the effect tipped back up so the streaks skim back over it
        /// (the prefab tips them down by KnightVfxBuilder.SpringTip).</summary>
        void KnightVfxSpring(SfFxEvent e)
        {
            var s = e.by;
            if (!KnightVfxOn || s == null || s.Pawn == null) return;
            Vector3 feet = Hooves(s.Pawn), back = -Kit.FlatDir(e.dir, Vector3.forward);
            Vector3 at = new Vector3(feet.x, e.at.y, feet.z);
            Vector3 way = Quaternion.AngleAxis(-SpringTip, Vector3.Cross(Vector3.up, back)) * back;
            var floor = SwordFightEdgeFloor.Current;
            if (floor != null && floor.Nearest(at, out var hit) && hit.distance < SpringLip)
            {
                at = hit.point + Vector3.up * (floor.Top - hit.point.y);
                way = hit.outward;
            }
            PieceVfx("Knight", "E_Spring", at, way);
        }

        /// <summary>E2's streaks' tip down, as KnightVfxBuilder.SpringTip (the builder is editor code).</summary>
        const float SpringTip = 40f;

        /// <summary>E3, over the somersault (e.at the floor where it springs, e.dir its way): the crescent its hind hooves
        /// draw, laid down where they really go a frame at a time from the turn over (SwordFightSkills.KnightTuckAt of the
        /// flight) until the buck as it lands, then left to die away from its tail. The hooves, not FlipPath: the way's
        /// ends are only known where it was played, and every screen moves the same hooves.</summary>
        void KnightVfxFlip(SfFxEvent e)
        {
            var s = e.by;
            if (!KnightVfxOn || s == null || s.Pawn == null) return;
            var go = PieceVfx("Knight", "E_Flip", e.at, Kit.FlatDir(e.dir, Vector3.forward));
            var arc = go != null ? go.GetComponentInChildren<VfxPathRibbon>() : null;
            if (arc == null) return;
            var pawn = s.Pawn;
            var X = EdgeS;
            float from = (X != null ? X.knightFlip : 0.8f) * SwordFightSkills.KnightTuckAt;
            // The prefab's typical way is for the workbench: this one is drawn from the hooves.
            arc.Begin();
            kit.Run(2f, (fx, d) =>
            {
                if (arc == null || s == null || pawn == null || s.Piece != PieceKind.Knight) return false;
                bool over = s.EdgeStage == SfEdge.Active, buck = s.EdgeStage == SfEdge.Recovery && s.EdgeTime < 0.09f;
                // Another screen may hear of the spring a moment before its stage turns: wait a little for it.
                if (!over && !buck) return s.EdgeStage == SfEdge.Windup && fx.age < 0.25f;
                if (buck || s.EdgeTime >= from) arc.Trace(Hooves(pawn));
                return true;
            });
        }

        /// <summary>E4, as its buck takes someone (only then: a kick that misses keeps the realistic flash and chips, 사용자
        /// 10-28 "맞았을 때만"): the star on the body where its hind hooves meet it, found as the realistic hit finds it
        /// (<see cref="OnBody"/> from the hooves), a hair off the body toward the knight, its +Z the way the body is
        /// kicked; the realistic horseshoe sparks go on under it.</summary>
        void KnightVfxKick(SfFxEvent e)
        {
            if (!KnightVfxOn || e.target == null) return;
            var knight = e.by != null ? e.by.Pawn : null;
            Vector3 at = OnBody(e.target, knight != null ? Hooves(knight) : e.at - e.dir, out Vector3 normal);
            PieceVfx("Knight", "E_Kick", at + normal * 0.05f, Kit.FlatDir(e.dir, -normal));
        }

        /// <summary>Halfway between its hind hooves.</summary>
        static Vector3 Hooves(RagdollPawn pawn) => Vector3.Lerp(pawn.bodies[(int)BodyId.FootL].position, pawn.bodies[(int)BodyId.FootR].position, 0.5f);
    }
}
