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
    /// One time of day everywhere (JY, later the same day: "1코스, 2코스, 3코스 다 시간은 그냥 아침이야. 맵을 진행할수록
    /// 해 위치가 변하는 기믹은 없어. 다 똑같은 시간대인데 2코스,3코스는 실내라 스카이 조명이나 선 라이트 영향을 좀
    /// 덜받는거지"): the sun, the sky and the haze stay the scene's morning; a place only lets less of the sky's light in.
    /// JY then: a map's time of day follows its concept ("각 맵마다 컨셉이 다르고, 그 맵에 어울리는 시간대로 달라질 수
    /// 있는데 아직은 아침 말고 다른 컨셉으로 가야하는게 천문대 말곤 없는거로 가자"): every map here is morning; the Royal
    /// Observatory, night, lights itself (RoyalObservatoryLighting). A map given another time later brings its own sun.
    ///   place   the team's zone light mixer (ZoneLight): the scene's own sun kept exactly, the scene's own ambient light
    ///           (the sky's) times the look's Indoor share and a little greyer (bounced off the walls), faded in over the
    ///           corridor as the camera comes in;
    ///   volume  the look's grade and light shafts, a global volume of the place's own, weighted the same way.
    /// Off with the upgrade (GameGfx).
    /// </summary>
    public static class GameCourses
    {
        public sealed class Look
        {
            public string Name;
            /// <summary>The share of the sky's light that gets in: the scene's own ambient light times this (the walls,
            /// the ceilings and the rig's lid keep the rest out). The sun is the scene's, never changed.</summary>
            public float Indoor = .5f;
            /// <summary>How much of that light has bounced off the room before it reaches anything: 0 the sky's own blue,
            /// 1 all of it the room's colour (BounceTint times its brightness).</summary>
            public float Bounce = .5f;
            /// <summary>The room's colour in its bounced light (its walls' and floors': warm for cream stone and wood, white
            /// for grey); kept near white (JY: shade, haze and grade never turn yellow).</summary>
            public Color BounceTint = Color.white;
            /// <summary>The grade: neutral by rule in a day hall (no warm or night cast; only light sources are warm).</summary>
            public float Exposure, Contrast = 24f, Saturation = 18f, BloomIntensity = .7f, BloomThreshold = .9f, Vignette = .15f;
            public float ShaftIntensity = 1.2f, ShaftDensity = .03f;
            public Color ShaftTint = new Color(1f, .98f, .95f);
            /// <summary>For a map whose concept is another time (the night observatory): white balance, the shadows' and
            /// highlights' tones (white: untouched; ShadowsLift raises the shadows), the bloom's spread (below 0: untouched)
            /// and tint (white: untouched).</summary>
            public float Temperature, Tint, ShadowsLift, BloomScatter = -1f;
            public Color ShadowsTone = Color.white, HighlightsTone = Color.white, BloomTint = Color.white;

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
            public bool shaftsOnly;
        }

        static readonly List<Place> places = new List<Place>();

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
        public static void Add(Transform frame, Bounds area, Look look) => Add(frame, area, look, true, true);

        /// <summary>light false: a section that lights itself (its own sun and ambient stay); grade false: only the look's
        /// light shafts over its own grade, true: the look's grade over it too.</summary>
        public static void Add(Transform frame, Bounds area, Look look, bool light, bool grade)
        {
            if (frame == null || look == null) return;
            foreach (var p in places) if (p.frame == frame) return;
            var place = new Place { frame = frame, area = area, look = look, zone = light ? ZoneLight.Add(look.Name + ": " + frame.name) : null };
#if CF_URP
            var go = new GameObject(look.Name + " grade (JY-gfx)");
            go.transform.SetParent(frame, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = Priority;
            volume.weight = 0f;
            place.shaftsOnly = !grade;
            place.profile = Profile(look, grade);
            volume.sharedProfile = place.profile;
            place.volume = volume;
#endif
            places.Add(place);
        }

#if CF_URP
        /// <summary>A look's grade and shafts over GameGrade's (only what a look changes); grade false: the shafts alone.</summary>
        public static VolumeProfile Profile(Look look, bool grade = true)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = look.Name;
            if (!grade)
            {
                var only = profile.Add<GfxLightShafts>(false);
                only.intensity.Override(look.ShaftIntensity);
                only.density.Override(look.ShaftDensity);
                only.tint.Override(look.ShaftTint);
                return profile;
            }
            var adjust = profile.Add<ColorAdjustments>(false);
            adjust.postExposure.Override(look.Exposure);
            adjust.contrast.Override(look.Contrast);
            adjust.saturation.Override(look.Saturation);
            var bloom = profile.Add<Bloom>(false);
            bloom.intensity.Override(look.BloomIntensity);
            bloom.threshold.Override(look.BloomThreshold);
            if (look.BloomScatter >= 0f) bloom.scatter.Override(look.BloomScatter);
            if (look.BloomTint != Color.white) bloom.tint.Override(look.BloomTint);
            profile.Add<Vignette>(false).intensity.Override(look.Vignette);
            if (look.Temperature != 0f || look.Tint != 0f)
            {
                var balance = profile.Add<WhiteBalance>(false);
                balance.temperature.Override(look.Temperature);
                balance.tint.Override(look.Tint);
            }
            if (look.ShadowsTone != Color.white || look.HighlightsTone != Color.white || look.ShadowsLift != 0f)
            {
                var tones = profile.Add<ShadowsMidtonesHighlights>(false);
                tones.shadows.Override(new Vector4(look.ShadowsTone.r, look.ShadowsTone.g, look.ShadowsTone.b, look.ShadowsLift));
                tones.highlights.Override(new Vector4(look.HighlightsTone.r, look.HighlightsTone.g, look.HighlightsTone.b, 0f));
            }
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
                p.profile = Profile(p.look, !p.shaftsOnly);
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
                    p.zone?.Remove();
#if CF_URP
                    if (p.profile != null) Object.Destroy(p.profile);
#endif
                    places.RemoveAt(i);
                    continue;
                }
                var look = p.look;
                var z = p.zone;
                if (z == null)
                {
                    // Shafts only (a section that lights itself): the volume by where the camera is, no light of ours.
                    float inside = wanted ? Mathf.Max(0f, ZoneLight.Inside(p.frame, p.area, Blend)) : 0f;
#if CF_URP
                    if (p.volume != null) p.volume.weight = inside;
#endif
                    continue;
                }
                // The scene's sun, sky and haze as they are (one time of day); less of the sky's light inside.
                z.KeepSun = true;
                z.TurnSun = false;
                z.AmbientScale = Mathf.Max(0f, look.Indoor);
                z.AmbientNeutral = look.Bounce;
                z.AmbientTint = look.BounceTint;
                z.Fog = false;
                float w = z.Ease(wanted ? ZoneLight.Inside(p.frame, p.area, Blend) : 0f);
#if CF_URP
                if (p.volume != null) p.volume.weight = w;
#endif
                if (w > best) { best = w; top = look; }
            }
            Weight = best;
            Current = top != null ? top.Name : "";
        }

        sealed class Watch : MonoBehaviour
        {
            void LateUpdate() => Follow();
        }
    }
}
