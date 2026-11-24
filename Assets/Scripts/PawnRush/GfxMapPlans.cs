using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    /// <summary>
    /// JY-gfx (2026-11-24): each indoor map's lighting design, light by light (JY: "조명 설계부터 다시 하고 라이팅
    /// 구현해"). Made by ArtSource/Gfx/Lighting/gen_plans.py from the designs beside it (one JSON per map); edit those
    /// and generate again rather than editing this file. GfxMapLights.Fixture says what each field means; the rig
    /// builds the lights with the section and scales each layer by its numbers (GfxMapLights.Rig).
    /// </summary>
    public static class GfxMapPlans
    {
        public static List<GfxMapLights.Fixture> For(string key) => key != null && Plans.TryGetValue(key, out var plan) ? plan : null;

        static readonly Dictionary<string, List<GfxMapLights.Fixture>> Plans = new Dictionary<string, List<GfxMapLights.Fixture>>
        {
        };
    }
}
