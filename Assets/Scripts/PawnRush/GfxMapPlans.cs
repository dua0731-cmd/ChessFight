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
            ["3-2_RoyalObservatory"] = new List<GfxMapLights.Fixture>
            {
                new GfxMapLights.Fixture { Layer = "accent", Note = "entry floor, lit from above", Type = LightType.Spot, At = new Vector3(0f, 10f, 7f), Aim = new Vector3(0f, -1f, 0f), Angle = 50f, Inner = 30f, Intensity = 380f, Range = 22f, Color = new Color(0.95f, 0.96f, 1f), Bounce = 0.35f },
                new GfxMapLights.Fixture { Layer = "accent", Note = "Dipper star 1", Type = LightType.Spot, At = new Vector3(-3f, 10f, 19f), Aim = new Vector3(0f, -1f, 0f), Angle = 50f, Inner = 30f, Intensity = 380f, Range = 22f, Color = new Color(0.95f, 0.96f, 1f), Bounce = 0.35f },
                new GfxMapLights.Fixture { Layer = "accent", Note = "Dipper star 2", Type = LightType.Spot, At = new Vector3(-9f, 11f, 25f), Aim = new Vector3(0f, -1f, 0f), Angle = 50f, Inner = 30f, Intensity = 380f, Range = 22f, Color = new Color(0.95f, 0.96f, 1f), Bounce = 0.35f },
                new GfxMapLights.Fixture { Layer = "accent", Note = "Dipper star 3", Type = LightType.Spot, At = new Vector3(-16f, 12f, 31f), Aim = new Vector3(0f, -1f, 0f), Angle = 50f, Inner = 30f, Intensity = 380f, Range = 22f, Color = new Color(0.95f, 0.96f, 1f), Bounce = 0.35f },
                new GfxMapLights.Fixture { Layer = "accent", Note = "between stars 3 and 4", Type = LightType.Spot, At = new Vector3(-23f, 12.5f, 38f), Aim = new Vector3(0f, -1f, 0f), Angle = 50f, Inner = 30f, Intensity = 380f, Range = 22f, Color = new Color(0.95f, 0.96f, 1f), Bounce = 0.35f },
                new GfxMapLights.Fixture { Layer = "accent", Note = "Dipper star 4", Type = LightType.Spot, At = new Vector3(-26.5f, 12.5f, 44f), Aim = new Vector3(0f, -1f, 0f), Angle = 50f, Inner = 30f, Intensity = 380f, Range = 22f, Color = new Color(0.95f, 0.96f, 1f), Bounce = 0.35f },
                new GfxMapLights.Fixture { Layer = "accent", Note = "between stars 4 and 7", Type = LightType.Spot, At = new Vector3(-26f, 13f, 52f), Aim = new Vector3(0f, -1f, 0f), Angle = 50f, Inner = 30f, Intensity = 380f, Range = 22f, Color = new Color(0.95f, 0.96f, 1f), Bounce = 0.35f },
                new GfxMapLights.Fixture { Layer = "accent", Note = "Dipper star 7", Type = LightType.Spot, At = new Vector3(-23.5f, 13.5f, 60.5f), Aim = new Vector3(0f, -1f, 0f), Angle = 50f, Inner = 30f, Intensity = 380f, Range = 22f, Color = new Color(0.95f, 0.96f, 1f), Bounce = 0.35f },
                new GfxMapLights.Fixture { Layer = "accent", Note = "the approach to the globe", Type = LightType.Spot, At = new Vector3(-15f, 14f, 63f), Aim = new Vector3(0f, -1f, 0f), Angle = 50f, Inner = 30f, Intensity = 380f, Range = 22f, Color = new Color(0.95f, 0.96f, 1f), Bounce = 0.35f },
                new GfxMapLights.Fixture { Layer = "accent", Note = "the crossing through the great globe", Type = LightType.Spot, At = new Vector3(0f, 20f, 64f), Aim = new Vector3(0f, -1f, 0f), Angle = 50f, Inner = 30f, Intensity = 380f, Range = 26f, Color = new Color(0.95f, 0.96f, 1f), Bounce = 0.35f },
                new GfxMapLights.Fixture { Layer = "accent", Note = "the east threshold", Type = LightType.Spot, At = new Vector3(15f, 22f, 65f), Aim = new Vector3(0f, -1f, 0f), Angle = 50f, Inner = 30f, Intensity = 380f, Range = 22f, Color = new Color(0.95f, 0.96f, 1f), Bounce = 0.35f },
                new GfxMapLights.Fixture { Layer = "accent", Note = "the east path, first bend", Type = LightType.Spot, At = new Vector3(21f, 22f, 73f), Aim = new Vector3(0f, -1f, 0f), Angle = 50f, Inner = 30f, Intensity = 380f, Range = 22f, Color = new Color(0.95f, 0.96f, 1f), Bounce = 0.35f },
                new GfxMapLights.Fixture { Layer = "accent", Note = "the east path, middle", Type = LightType.Spot, At = new Vector3(21f, 22f, 82f), Aim = new Vector3(0f, -1f, 0f), Angle = 50f, Inner = 30f, Intensity = 380f, Range = 22f, Color = new Color(0.95f, 0.96f, 1f), Bounce = 0.35f },
                new GfxMapLights.Fixture { Layer = "accent", Note = "the east path, last bend", Type = LightType.Spot, At = new Vector3(20f, 22f, 91f), Aim = new Vector3(0f, -1f, 0f), Angle = 50f, Inner = 30f, Intensity = 380f, Range = 22f, Color = new Color(0.95f, 0.96f, 1f), Bounce = 0.35f },
                new GfxMapLights.Fixture { Layer = "accent", Note = "the stage deck", Type = LightType.Spot, At = new Vector3(9f, 22f, 91.5f), Aim = new Vector3(0f, -1f, 0f), Angle = 50f, Inner = 30f, Intensity = 380f, Range = 22f, Color = new Color(0.95f, 0.96f, 1f), Bounce = 0.35f },
                new GfxMapLights.Fixture { Layer = "accent", Note = "the ramp up to the balcony", Type = LightType.Spot, At = new Vector3(9f, 27f, 105f), Aim = new Vector3(0f, -1f, 0f), Angle = 50f, Inner = 30f, Intensity = 380f, Range = 24f, Color = new Color(0.95f, 0.96f, 1f), Bounce = 0.35f },
                new GfxMapLights.Fixture { Layer = "accent", Note = "the balcony and the way out", Type = LightType.Spot, At = new Vector3(9f, 26f, 124f), Aim = new Vector3(0f, -1f, 0f), Angle = 50f, Inner = 30f, Intensity = 380f, Range = 22f, Color = new Color(0.95f, 0.96f, 1f), Bounce = 0.35f },
                new GfxMapLights.Fixture { Layer = "decorative", Note = "display niches (JY: '전시장 위에 조명'): a small warm spot above and in front of each statue, aimed back at it", Type = LightType.Spot, Each = "Statue", Anchor = "top", Offset = new Vector3(1.2f, 1.4f, 0f), Radial = true, Max = 96, Spacing = 1f, Aim = new Vector3(-0.4f, -1f, 0f), Angle = 44f, Inner = 22f, Intensity = 15f, Range = 7f, Color = new Color(1f, 0.84f, 0.64f), Bounce = 0.6f },
            },
        };
    }
}
