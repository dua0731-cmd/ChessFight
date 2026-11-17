using UnityEngine;

namespace ChessFight.PawnRush
{
    // 2026-11-17 (승규 "4·5번 물레방아·물" - the wheels turning, the pouring water and falls, the flowing water - made with
    // the ElevenLabs MCP): the yard's standing loops, under "Yard sounds" beside "Built" (YardSoundscape). Baked by
    // ArtSource/PawnRush/Sfx/bake_carriage_yard.py into Resources/PawnRushCarriageYardSfx (prompts:
    // Docs/PawnRush/PAWN_RUSH_AUDIO.md section 14). Switching that object off silences them; nothing else changes.
    public sealed partial class RoyalCarriageYard
    {
        void Sounds()
        {
            if (root == null) return;
            var go = new GameObject("Yard sounds");
            go.transform.SetParent(transform, false);
            var scape = go.AddComponent<YardSoundscape>();
            // The canal and the mill pond, in this object's frame (the pond's in the later sections' frame, moved).
            scape.Configure(transform,
                new[]
                {
                    new Bounds(new Vector3(0f, CanalWater, (CanalZ0 + CanalZ1) * .5f), new Vector3(YardHalf * 2f, 0f, CanalZ1 - CanalZ0)),
                    new Bounds(new Vector3(0f, -.5f, (PoolZ0 + CliffZ) * .5f + LaterShift), new Vector3(SortHalf * 2f, 0f, CliffZ - PoolZ0)),
                });
        }
    }
}
