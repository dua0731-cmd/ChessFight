using ChessFight.VFX;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The queen's F and E effects over her realistic drawing (R162, 10-27, 사용자: "vfx 퀸 작업 … 1번째 사진 퀸이 꼬치
    /// 베기를 했을 때 그게 잔상이였으면 … E F 스킬 적용"; R164: "흰 노란이 아니라 검은색과 빨간색(완전한 빨간색 x)이 바닥에
    /// 그어지고 잔상도 다시 디자인해줘"): the way she went is burnt into the floor (<see cref="VfxBeam"/> on the floor: black
    /// char with dark red embers, from where she started to under her, cooling and fading once she stops), and her
    /// afterimages are dark copies of her body with a dark red rim that burn away (<see cref="VfxPhantom"/>), in place of
    /// the see-through copies (<see cref="QueenGhosts"/> brings those back as well). The prefabs of
    /// Assets/VFX/Prefabs/Queen listed in Resources/VfxSets/Queen: the thrust (F_Thrust: the burn line, a dark ring where
    /// she pushes off, a dark red flash), each cut on her way (F_Hit), the checkmate cut's crossing (E_Blink: the same
    /// line all at once), the cut landing a beat later (E_Cut) and one afterimage (Phantom). Every screen draws them (they
    /// hang off the same events). <see cref="QueenVfxOn"/> false leaves the realistic drawing as it was.
    /// </summary>
    public partial class SwordFightSkillFx
    {
        public static bool QueenVfxOn = true;
        /// <summary>Leave her see-through copies (10-25) along with the dark ones.</summary>
        public static bool QueenGhosts;

        static bool QueenCopies => !QueenVfxOn || QueenGhosts;

        /// <summary>Plays the queen's effect <paramref name="key"/> at <paramref name="at"/>, its +Z along
        /// <paramref name="forward"/>; the copy, or null.</summary>
        GameObject QueenVfx(string key, Vector3 at, Vector3 forward) => QueenVfxOn ? SetVfx("Queen", key, at, forward) : null;

        /// <summary>Plays <paramref name="piece"/>'s effect <paramref name="key"/> (as <see cref="PieceVfx"/>, but handing
        /// back the copy, or null, for its parts: a burn line to move, an afterimage to give a shape).</summary>
        GameObject SetVfx(string piece, string key, Vector3 at, Vector3 forward)
        {
            var set = VfxSet.Load(piece);
            if (set == null) return null;
            var facing = forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward) : Quaternion.identity;
            return set.Play(key, at, facing, kit.root);
        }

        /// <summary>The burn line in an effect just played, or null.</summary>
        static VfxBeam BeamOf(GameObject fx) => fx != null ? fx.GetComponentInChildren<VfxBeam>(true) : null;

        /// <summary>A dark copy of her as she is now (Phantom), moved by <paramref name="shift"/> (where she was a moment
        /// ago), drifting back against <paramref name="way"/>, living and as solid as the scales say.</summary>
        void QueenPhantom(RagdollPawn pawn, Vector3 way, Vector3 shift = default, float lifeScale = 1f, float opacityScale = 1f)
        {
            var skin = DrawnSkin(pawn);
            if (skin == null) return;
            var fx = QueenVfx("Phantom", pawn.Hips.position + shift, way);
            var phantom = fx != null ? fx.GetComponentInChildren<VfxPhantom>(true) : null;
            if (phantom != null) phantom.Take(skin, shift, lifeScale, opacityScale);
        }
    }
}
