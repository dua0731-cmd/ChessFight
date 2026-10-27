using ChessFight.VFX;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The rook's F in the queen's look (R165, 10-27, 사용자: "퀸 지금 너무 괜찮다 룩은 f 스킬을 쓸 때 퀸 처럼 vfx 만들어주고
    /// 파편 조각을 줄여줘", then "색은 룩이 지금 고유색으로 사용하고 있는 색으로 해야지"): the queen's shapes in the rook's
    /// own colour - the beige of <see cref="BOrange"/> (#D3B596) glowing round a char of its deep tone burnt almost black.
    /// The slam leaves a scorched ring and a beige flash (F_Slam), the wave burns a line into the board down its middle as
    /// it runs (F_Line, a <see cref="VfxBeam"/> on the floor), each square a tower bursts from is scorched round (F_Stamp),
    /// each tower leaves a dark copy of itself burning away as it sinks (F_Phantom, a <see cref="VfxPhantom"/>), and
    /// whoever the wave throws gets a flash and a few sparks (F_Hit). The prefabs of Assets/VFX/Prefabs/Rook listed in
    /// Resources/VfxSets/Rook (the edge skill's E_Break is the king and rook session's). Fewer bits as well: the board's
    /// splinters, the towers' rubble and the chips off whoever is thrown are cut to <see cref="RookDebris"/> of what they
    /// were, the biggest stones kept. <see cref="RookVfxOn"/> false leaves the realistic drawing alone (the cut in bits
    /// stays).
    /// </summary>
    public partial class SwordFightSkillFx
    {
        /// <summary>Share of the rook's F splinters, rubble and chips that is kept (R165: "파편 조각을 줄여줘").</summary>
        public static float RookDebris = .35f;

        static int RookChips(int n) => Mathf.Max(1, Mathf.RoundToInt(n * RookDebris));

        /// <summary>Plays the rook's effect <paramref name="key"/>; the copy, or null.</summary>
        GameObject RookFVfx(string key, Vector3 at, Vector3 forward) => RookVfxOn ? SetVfx("Rook", key, at, forward) : null;

        /// <summary>A dark copy of a tower as it stands now, burning away where it stood.</summary>
        void RookPhantom(Renderer tower, Vector3 forward)
        {
            if (tower == null) return;
            var fx = RookFVfx("F_Phantom", tower.transform.position, forward);
            var phantom = fx != null ? fx.GetComponentInChildren<VfxPhantom>(true) : null;
            if (phantom != null) phantom.Take(tower);
        }
    }
}
