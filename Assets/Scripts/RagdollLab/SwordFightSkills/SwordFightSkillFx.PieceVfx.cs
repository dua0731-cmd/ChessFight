using System.Collections.Generic;
using ChessFight.Network;
using ChessFight.VFX;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// A light layer of effects over the realistic F and E skills, one piece at a time (R159 · R160, 10-27, 사용자: "킹부터
    /// 작업진행해주고 F, E스킬 적용 … 너무 화려하게 안해줬으면", then "룩도"): thin rings, small flashes, a few sparks, the
    /// prefabs of Assets/VFX/Prefabs/&lt;Piece&gt; listed in Resources/VfxSets/&lt;Piece&gt;. Kept dim and short so the steel,
    /// the stone, the air and the shake stay the main thing. Every screen draws them (they hang off the same events).
    /// <list type="bullet">
    /// <item>King, in his gold: the stance (F_Guard), the block (F_Block), the counter (F_Counter), whoever it strikes
    /// (F_Hit, R187), the guard that came to nothing (F_Whiff, R193), the sword into the wall (E_Stab, R196), the crown
    /// foretold while he hangs (E_Gather, R199), the spring off it (E_Vault, R200), the landing (E_Land, R201), whoever it
    /// throws (E_Hit, R202), the fall (E_Drop, R203).</item>
    /// <item>Rook, in warm stone: the slam (F_Slam), each square the wave raises a tower on (F_Stamp), whoever it throws
    /// (F_Hit), the blow on the lip with the crack and the fall of the chunk (E_Break).</item>
    /// </list>
    /// <see cref="KingVfxOn"/> / <see cref="RookVfxOn"/> false leave that piece's realistic drawing alone.
    /// </summary>
    public partial class SwordFightSkillFx
    {
        public static bool KingVfxOn = true, RookVfxOn = true;

        GameObject KingVfx(string key, Vector3 at, Vector3 forward) => KingVfxOn ? PieceVfx("King", key, at, forward) : null;

        GameObject RookVfx(string key, Vector3 at, Vector3 forward) => RookVfxOn ? PieceVfx("Rook", key, at, forward) : null;

        /// <summary>R190 (F1): his eight squares lit round him and his blade alight through the guard. The squares keep the
        /// way he faced as it started and step after him; both blink at its end and go out as it ends (a block, a whiff, or
        /// it ran out). Another screen may hear of the guard a moment before its stage turns: it waits a little for it.</summary>
        void KingGuardVfx(SwordFightSkills s)
        {
            var king = s != null ? s.Pawn : null;
            if (king == null) return;
            var go = KingVfx("F_Guard", Kit.FloorUnder(king.Hips.position, 1.5f), Kit.FlatDir(king.Facing, Vector3.forward));
            if (go == null) return;
            var grid = go.GetComponentInChildren<VfxSquareGrid>();
            var blade = go.GetComponentInChildren<VfxBladeGlow>();
            var P = s.S != null ? s.S : S;
            float guard = P != null ? P.kingGuard : 0.9f;
            // A square is drawn where there is floor at about his height (not over a drop). 10-28: on a board it is the
            // border of the board square he stands on (the board's own cell, not a square of the knight's measure).
            if (grid != null)
            {
                var arena = FindFirstObjectByType<SwordFightArena>();
                grid.Begin(P != null ? P.knightCell : 1.25f, p => Kit.FloorUnder(p, 0.6f, out var under) && Mathf.Abs(under.y - p.y) < 0.6f,
                    arena != null ? (BoardSquareFinder)arena.SquareAt : null);
                // The wave off the border rises as far as his belly (a little over his hips).
                grid.waveHeight = Mathf.Clamp(king.Hips.position.y - Kit.FloorUnder(king.Hips.position, 1.5f).y + 0.1f, 0.4f, 1.6f);
            }
            var f = s.Fighter;
            if (blade != null)
                blade.Follow((out Vector3 hilt, out Vector3 point) =>
                {
                    bool there = f != null && f.SwordBody != null;
                    hilt = there ? f.BladeRoot : Vector3.zero;
                    point = there ? f.BladeTip : Vector3.zero;
                    return there;
                });
            bool seen = false;
            kit.Run(guard + 3f, (fx, d) =>
            {
                bool on = go != null && s != null && s.Pawn != null && s.Piece == PieceKind.King && s.Stage == SfStage.Windup;
                if (!on && !seen && go != null && fx.age < 0.25f) return true;
                if (!on)
                {
                    if (grid != null) grid.Stop();
                    if (blade != null) blade.Stop();
                    return false;
                }
                seen = true;
                float left = guard - s.StageTime;
                if (grid != null) grid.Follow(Kit.FloorUnder(s.Pawn.Hips.position, 1.5f), left);
                if (blade != null) blade.Hold(left);
                return true;
            });
        }

        /// <summary>R193 (F5): the guard came to nothing. As his point drops to the board the gold left on his blade
        /// breaks up into a few motes that drift down off it, more of them off the point (F1's squares and glow go out as
        /// the guard ends). The two emitters (named as KingVfxBuilder names them) are kept on his blade wherever it is
        /// while it drops, so the motes are shed along it as it falls.</summary>
        void KingWhiffVfx(SfFxEvent e)
        {
            var king = e.by != null ? e.by.Pawn : null;
            var f = e.by != null ? e.by.Fighter : null;
            if (king == null || f == null || f.SwordBody == null) return;
            var go = KingVfx("F_Whiff", Kit.FloorUnder(king.Hips.position, 1.5f), Kit.FlatDir(king.Facing, Vector3.forward));
            if (go == null) return;
            Transform blade = go.transform.Find("F5 Motes"), point = go.transform.Find("F5 Point");
            bool OnBlade()
            {
                if (go == null || f == null || f.SwordBody == null) return false;
                Vector3 hilt = f.BladeRoot, tip = f.BladeTip;
                if (blade != null && (tip - hilt).sqrMagnitude > 1e-6f)
                    blade.SetPositionAndRotation((hilt + tip) * 0.5f, Quaternion.FromToRotation(Vector3.right, tip - hilt));
                if (point != null) point.position = tip;
                return true;
            }
            // Before its first frame too, so the first motes leave the blade and not where the workbench has it.
            OnBlade();
            kit.Run(0.5f, (fx, d) => OnBlade() && fx.age < 0.45f);
        }

        /// <summary>R196 (E1): the sword goes into the stone. A star where the blade meets it, and the cracks of the breach
        /// KingBreach has just broken glowing gold out from the rim, then cooling (the prefab's VfxCrackGlow, which the
        /// breach reads each frame). Both parts (named as KingVfxBuilder names them) go to that breach; with none (no
        /// floor found) the star stays by the hilt and nothing glows.</summary>
        void KingStabVfx(SwordFightSkills s, Vector3 at, Vector3 dir)
        {
            var go = KingVfx("E_Stab", at, dir);
            if (go == null) return;
            Transform star = go.transform.Find("E1 Star"), cracks = go.transform.Find("E1 Cracks");
            var glow = cracks != null ? cracks.GetComponent<VfxCrackGlow>() : null;
            if (glow != null) glow.Live();
            Breach g = null;
            for (int i = breaches.Count - 1; i >= 0 && g == null; i--)
                if (breaches[i].king == s && breaches[i].released < 0f) g = breaches[i];
            if (g == null) return;
            if (star != null) star.position = g.at + g.outward * 0.06f;
            if (cracks != null) cracks.SetPositionAndRotation(g.at, Quaternion.LookRotation(g.outward, Vector3.up));
            g.gild = glow;
        }

        readonly Dictionary<SwordFightSkills, VfxCrownMark> kingMarks = new Dictionary<SwordFightSkills, VfxCrownMark>();

        /// <summary>R199 (E2): as the sword goes home, the crown foretold on the spot he will land on, drawn in while he
        /// hangs, and a glow breathing at his hilt. It is ended by his landing (<see cref="KingMarkEnd"/>, flaring into
        /// the seal) or his fall (dying away); with neither (an event missed) it goes by itself.</summary>
        void KingMarkVfx(SwordFightSkills s)
        {
            var go = KingVfx("E_Gather", Kit.FloorUnder(s.EdgeLand + Vector3.up * 0.5f, 1f), -s.EdgeOut);
            var mark = go != null ? go.GetComponent<VfxCrownMark>() : null;
            if (mark == null) return;
            mark.Live();
            mark.Hilt(s.Hilt);
            kingMarks[s] = mark;
        }

        /// <summary>R199 (E2): he landed (it flares and is gone as E4's seal bursts) or fell (it dies away).</summary>
        void KingMarkEnd(SwordFightSkills s, bool landed)
        {
            if (s == null || !kingMarks.TryGetValue(s, out var mark)) return;
            kingMarks.Remove(s);
            if (mark == null) return;
            if (landed) mark.Land();
            else mark.Stop();
        }

        /// <summary>R200 (E3): he tears the sword out and springs. The burst and the spit (named as KingVfxBuilder names
        /// them) go to the crater he tore it out of; the trail is laid down from the crater along where his body goes,
        /// 0.2 m under his hips, a point a frame through the spring.</summary>
        void KingVaultVfx(SwordFightSkills s)
        {
            var pawn = s.Pawn;
            var go = KingVfx("E_Vault", pawn.Hips.position + Vector3.down * 0.45f, Kit.FlatDir(pawn.Facing, Vector3.forward));
            if (go == null) return;
            Breach g = null;
            for (int i = breaches.Count - 1; i >= 0 && g == null; i--)
                if (breaches[i].king == s) g = breaches[i];
            Vector3 outward = g != null ? g.outward : s.EdgeOut;
            Vector3 crater = (g != null ? g.at : s.Hilt - s.EdgeOut * 0.15f) + outward * 0.05f;
            Transform burst = go.transform.Find("E3 Burst"), spit = go.transform.Find("E3 Spit"), path = go.transform.Find("E3 Trail");
            if (burst != null) burst.position = crater;
            if (spit != null) spit.SetPositionAndRotation(crater, Quaternion.LookRotation(outward, Vector3.up));
            var trail = path != null ? path.GetComponent<VfxPathRibbon>() : null;
            if (trail == null) return;
            trail.Begin();
            trail.Trace(crater);
            kit.Run(0.7f, (fx, d) =>
            {
                if (trail == null || pawn == null) return false;
                trail.Trace(pawn.Hips.position + Vector3.down * 0.2f);
                return fx.age < 0.55f;
            });
        }

        /// <summary>R202 (E5): on whoever the landing throws, a star, a short streak slanting across him and slivers the way
        /// he goes, hung on his chest so it goes with him (as F4's cut is).</summary>
        void KingLandHitVfx(SfFxEvent e)
        {
            if (e.target == null) return;
            var chest = e.target.bodies[(int)BodyId.Chest];
            var hit = KingVfx("E_Hit", chest.position, e.dir);
            if (hit == null) return;
            // The streak lies in its own XY: turned square to this screen's view so it slants across him from any side
            // (the landing throws them every way; F4's cut faces the king, behind whom the view is).
            var streak = hit.transform.Find("E5 Streak");
            var eye = Camera.main;
            if (streak != null && eye != null)
            {
                Vector3 away = chest.position - eye.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude > 1e-4f) streak.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
            }
            hit.transform.SetParent(chest.transform, true);
        }

        /// <summary>R203 (E6): his sword is knocked loose and he falls. The gold goes out - the cracks' glow quenched, the
        /// crown dying away - and a few gold motes break off the hilt and the crater and fall with him.</summary>
        void KingDropVfx(SwordFightSkills s, SfFxEvent e)
        {
            KingMarkEnd(s, false);
            for (int i = breaches.Count - 1; i >= 0; i--)
                if (breaches[i].king == s && breaches[i].released < 0f && breaches[i].gild != null) breaches[i].gild.Quench();
            KingVfx("E_Drop", e.at, e.dir);
        }

        /// <summary>R187 (F4): the gold cut across the front of whoever the counter struck, with slivers and air the way he
        /// is pushed. e.at is between the two of them, so it goes on his chest instead, hung on it so it goes with him (as
        /// the bishop's F7 does).</summary>
        void KingHitVfx(SfFxEvent e)
        {
            if (e.target == null) return;
            var chest = e.target.bodies[(int)BodyId.Chest];
            var cut = KingVfx("F_Hit", chest.position, e.dir);
            if (cut != null) cut.transform.SetParent(chest.transform, true);
        }

        /// <summary>Plays <paramref name="piece"/>'s effect <paramref name="key"/> at <paramref name="at"/>, its +Z along
        /// <paramref name="forward"/>; the copy, or null.</summary>
        GameObject PieceVfx(string piece, string key, Vector3 at, Vector3 forward)
        {
            var set = VfxSet.Load(piece);
            if (set == null) return null;
            var facing = forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward) : Quaternion.identity;
            return set.Play(key, at, facing, kit.root);
        }
    }
}
