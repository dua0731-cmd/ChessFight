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
    /// (F_Hit, R187), the guard that came to nothing (F_Whiff, R193), the sword into the wall (E_Stab), the spring off it (E_Vault), the landing (E_Land).</item>
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
            // A square is drawn where there is floor at about his height (not over a drop).
            if (grid != null)
                grid.Begin(P != null ? P.knightCell : 1.25f, p => Kit.FloorUnder(p, 0.6f, out var under) && Mathf.Abs(under.y - p.y) < 0.6f);
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
