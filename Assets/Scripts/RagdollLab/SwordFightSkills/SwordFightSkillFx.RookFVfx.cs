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

        /// <summary>How big the towers' rubble stones are against the model's own (10-28, 승규 님: "룩 f 스킬 쓸 때 파편
        /// 크길 줄여주고", then "더 줄이고": half, then 0.3).</summary>
        public static float RookRubbleSize = .3f;

        /// <summary>The rook F's dust (its own real dust: the kit's puffs are off in this mode).</summary>
        SkillDust jetDust;

        /// <summary>10-28 (승규 님: "먼지가 뿜어져 나오게 해", "먼지가 많아 줄여", then "먼지는 바닥에 좌악 깔려야돼"): a thin
        /// layer of real dust (<see cref="SkillDust"/>) swept out flat over the board from <paramref name="at"/> all round,
        /// <paramref name="n"/> low billows out to about <paramref name="radius"/>, hugging the floor, spreading wide and
        /// thinning as they slow, the board's wood in their grey.</summary>
        void DustSpread(Vector3 at, int n, float radius, float size, Color wood)
        {
            var dust = jetDust;
            if (dust == null || !dust.Ready || n <= 0) return;
            SkillDust.Tint(Color.Lerp(wood, Color.white, 0.4f), wood * 0.45f, out var lit, out var shade, 0.2f);
            float floor = SkillDust.FloorY(at + Vector3.up * 0.2f);
            if (float.IsNegativeInfinity(floor)) floor = at.y;
            float start = Random.Range(0f, 360f);
            for (int i = 0; i < n; i++)
            {
                float ang = (start + (i + Random.Range(-0.4f, 0.4f)) * 360f / n) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                float s = size * Random.Range(0.7f, 1.1f);
                // Flat out (its speed is how far it goes before the air stops it), barely lifting: a sheet along the floor.
                dust.Emit(new Vector3(at.x, floor + s * 0.3f, at.z) + dir * (s * 0.3f), dir * (radius * Random.Range(0.7f, 1.1f) * 4.5f) + Vector3.up * 0.05f,
                          s, Random.Range(0.7f, 1.1f), lit, shade, Random.Range(1.6f, 2.1f), Random.Range(0f, 0.05f), Random.Range(0.25f, 0.35f), floor);
            }
        }

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
