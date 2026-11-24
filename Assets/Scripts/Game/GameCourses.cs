using System.Collections.Generic;
using UnityEngine;
#if CF_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif
using UnityEngine.SceneManagement;

namespace ChessFight.Game
{
    /// <summary>
    /// JY-gfx (2026-11-24): the light of Pawn Rush's indoor maps, one place per section, each with a look of its own.
    /// JY: courses 2 and 3 are indoors and need light that fits each space ("2코스는 ... 멋진 성 안으로 들어갓다는 느김",
    /// "3코스는 엄중하고, 압도적이거나 웅장하고, 게임의 결말같은"), and then: "3코스도 각 맵들마다 컨셉이 아에 다른데
    /// 획일화되잖아" - so every map brings its own look (the PawnRush side, GfxMapLights, picks it by the map's name and
    /// builds its fixtures; this class carries the scene-wide part):
    ///   place   the team's zone light mixer (ZoneLight): the look's sun, turned in the section's own frame (x right, y
    ///           up, z along the route) so it comes through that map's windows however the round laid it, its ambient
    ///           light and its haze, faded in over the corridor as the camera comes in;
    ///   volume  the look's grade and light shafts, a global volume of the place's own, weighted the same way;
    ///   sky     the sky over the roofless halls times the look's tint.
    /// Off with the upgrade (GameGfx).
    /// </summary>
    public static class GameCourses
    {
        public sealed class Look
        {
            public string Name;
            /// <summary>The way the sun's light travels in the section's frame (x right, y up, z along the route).</summary>
            public Vector3 SunWay = new Vector3(.5f, -.7f, -.5f);
            public Color SunColor = Color.white;
            public float SunIntensity = 1.5f, ShadowStrength = 1f;
            /// <summary>The ambient light from above, the sides and below (sRGB, the gradient mode's).</summary>
            public Color Sky = new Color(.5f, .5f, .52f), Equator = new Color(.45f, .44f, .44f), Ground = new Color(.25f, .24f, .23f);
            public Color FogColor = new Color(.7f, .7f, .72f);
            public float FogStart = 30f, FogEnd = 300f;
            public float Exposure, Contrast = 24f, Saturation = 18f, Temperature, BloomIntensity = .7f, BloomThreshold = .9f, Vignette = .15f;
            public float ShaftIntensity = 1.2f, ShaftDensity = .03f;
            public Color ShaftTint = new Color(1f, .97f, .92f);
            /// <summary>The sky over the roofless halls, times this.</summary>
            public Color SkyTint = Color.white;

            public Look Copy(string name)
            {
                var c = (Look)MemberwiseClone();
                c.Name = name;
                return c;
            }
        }

        public static bool On = true;
        /// <summary>Metres out of a section's box over which its look fades (then eased over about a second).</summary>
        public static float Blend = 12f;
        const int Priority = 25;

        sealed class Place
        {
            public Transform frame;
            public Bounds area;
            public Look look;
            public ZoneLight zone;
#if CF_URP
            public Volume volume;
            public VolumeProfile profile;
#endif
        }

        static readonly List<Place> places = new List<Place>();
        static readonly int TintId = Shader.PropertyToID("_Tint");

        /// <summary>The look the camera is most inside now, and how far (0 to 1).</summary>
        public static string Current { get; private set; } = "";
        public static float Weight { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            places.Clear();
            var go = new GameObject("Gfx courses (JY-gfx)") { hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Watch>();
        }

        /// <summary>A section with a look of its own: its frame, its box in that frame, the look (kept, so changing its
        /// numbers changes the section; call Refresh for the grade's).</summary>
        public static void Add(Transform frame, Bounds area, Look look)
        {
            if (frame == null || look == null) return;
            foreach (var p in places) if (p.frame == frame) return;
            var place = new Place { frame = frame, area = area, look = look, zone = ZoneLight.Add(look.Name + ": " + frame.name) };
#if CF_URP
            var go = new GameObject(look.Name + " grade (JY-gfx)");
            go.transform.SetParent(frame, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = Priority;
            volume.weight = 0f;
            place.profile = Profile(look);
            volume.sharedProfile = place.profile;
            place.volume = volume;
#endif
            places.Add(place);
        }

#if CF_URP
        /// <summary>A look's grade and shafts over GameGrade's (only what a look changes).</summary>
        public static VolumeProfile Profile(Look look)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = look.Name;
            var adjust = profile.Add<ColorAdjustments>(false);
            adjust.postExposure.Override(look.Exposure);
            adjust.contrast.Override(look.Contrast);
            adjust.saturation.Override(look.Saturation);
            profile.Add<WhiteBalance>(false).temperature.Override(look.Temperature);
            var bloom = profile.Add<Bloom>(false);
            bloom.intensity.Override(look.BloomIntensity);
            bloom.threshold.Override(look.BloomThreshold);
            profile.Add<Vignette>(false).intensity.Override(look.Vignette);
            var shafts = profile.Add<GfxLightShafts>(false);
            shafts.intensity.Override(look.ShaftIntensity);
            shafts.density.Override(look.ShaftDensity);
            shafts.tint.Override(look.ShaftTint);
            return profile;
        }
#endif

        /// <summary>The looks' numbers changed (the picture tool): every place's grade made again.</summary>
        public static void Refresh()
        {
#if CF_URP
            foreach (var p in places)
            {
                if (p.volume == null) continue;
                if (p.profile != null) Object.Destroy(p.profile);
                p.profile = Profile(p.look);
                p.volume.sharedProfile = p.profile;
            }
#endif
        }

        static void Follow()
        {
            bool wanted = On && GameGfx.Upgrade && GameGfx.InScope();
            float best = 0f;
            Look top = null;
            for (int i = places.Count - 1; i >= 0; i--)
            {
                var p = places[i];
                if (p.frame == null)
                {
                    p.zone.Remove();
#if CF_URP
                    if (p.profile != null) Object.Destroy(p.profile);
#endif
                    places.RemoveAt(i);
                    continue;
                }
                var look = p.look;
                var z = p.zone;
                z.TurnSun = true;
                z.SunRotation = Quaternion.LookRotation(p.frame.TransformDirection(look.SunWay.normalized), Vector3.up);
                z.SunColor = look.SunColor;
                z.SunIntensity = look.SunIntensity;
                z.ShadowStrength = look.ShadowStrength;
                z.Sky = look.Sky;
                z.Equator = look.Equator;
                z.Ground = look.Ground;
                z.Fog = true;
                z.FogColor = look.FogColor;
                z.FogStart = look.FogStart;
                z.FogEnd = look.FogEnd;
                float w = z.Ease(wanted ? ZoneLight.Inside(p.frame, p.area, Blend) : 0f);
#if CF_URP
                if (p.volume != null) p.volume.weight = w;
#endif
                if (w > best) { best = w; top = look; }
            }
            Weight = best;
            Current = top != null ? top.Name : "";
            // The sky over the roofless halls (GameSky's own copy of the sky material only).
            var sky = RenderSettings.skybox;
            if (sky != null && sky.name == "Gfx sky" && sky.HasProperty(TintId))
                sky.SetColor(TintId, top != null ? Color.Lerp(Color.white, top.SkyTint, best) : Color.white);
        }

        sealed class Watch : MonoBehaviour
        {
            void LateUpdate() => Follow();
        }
    }
}
