using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The bishop's F and E effects over his realistic drawing (R167, 10-27, 사용자: "비숍은 거의 vfx가 없으니까 만들어줘
    /// 리얼리스틱하게 안해줘도 돼"), made layer by layer by the editor menu ChessFight/VFX/Bishop/Build
    /// (Assets/Scripts/VFXEditor/BishopVfxBuilder.cs) into Assets/VFX/Prefabs/Bishop, listed in Resources/VfxSets/Bishop.
    /// Until the menu has been run there is no set and nothing plays. So far: the X sigil where the stakes drive in
    /// (F_Pin). Every screen draws them (they hang off the same moments). <see cref="BishopVfxOn"/> false leaves the
    /// realistic drawing alone.
    /// </summary>
    public partial class SwordFightSkillFx
    {
        public static bool BishopVfxOn = true;

        void BishopVfx(string key, Vector3 at, Vector3 forward)
        {
            if (BishopVfxOn) PieceVfx("Bishop", key, at, forward);
        }
    }
}
