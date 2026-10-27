using ChessFight.VFX;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// A light layer of effects over the realistic F and E skills, one piece at a time (R159 · R160, 10-27, 사용자: "킹부터
    /// 작업진행해주고 F, E스킬 적용 … 너무 화려하게 안해줬으면", then "룩도"): thin rings, small flashes, a few sparks, the
    /// prefabs of Assets/VFX/Prefabs/&lt;Piece&gt; listed in Resources/VfxSets/&lt;Piece&gt;. Kept dim and short so the steel,
    /// the stone, the air and the shake stay the main thing. Every screen draws them (they hang off the same events).
    /// <list type="bullet">
    /// <item>King, in his gold: the stance (F_Guard), the block (F_Block), the counter (F_Counter), the sword into the wall
    /// (E_Stab), the spring off it (E_Vault), the landing (E_Land).</item>
    /// <item>Rook, in warm stone: the slam (F_Slam), each square the wave raises a tower on (F_Stamp), whoever it throws
    /// (F_Hit), the blow on the lip with the crack and the fall of the chunk (E_Break).</item>
    /// </list>
    /// <see cref="KingVfxOn"/> / <see cref="RookVfxOn"/> false leave that piece's realistic drawing alone.
    /// </summary>
    public partial class SwordFightSkillFx
    {
        public static bool KingVfxOn = true, RookVfxOn = true;

        void KingVfx(string key, Vector3 at, Vector3 forward)
        {
            if (KingVfxOn) PieceVfx("King", key, at, forward);
        }

        void RookVfx(string key, Vector3 at, Vector3 forward)
        {
            if (RookVfxOn) PieceVfx("Rook", key, at, forward);
        }

        /// <summary>Plays <paramref name="piece"/>'s effect <paramref name="key"/> at <paramref name="at"/>, its +Z along
        /// <paramref name="forward"/>.</summary>
        void PieceVfx(string piece, string key, Vector3 at, Vector3 forward)
        {
            var set = VfxSet.Load(piece);
            if (set == null) return;
            var facing = forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward) : Quaternion.identity;
            set.Play(key, at, facing, kit.root);
        }
    }
}
