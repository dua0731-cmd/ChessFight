#if CF_URP
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ChessFight.Game
{
    /// <summary>
    /// JY-gfx (2026-11-24, the graphics upgrade, target Mario Kart World): the game's grade in play. One global volume
    /// in every scene past the title, over the profile (Settings/GameLook.asset) and GameLook's softer one (priority
    /// 10), under the settings' no-bloom and colour-aid ones (1000, 1001): a tonemapper that keeps hues clean and
    /// saturated (Neutral; ACES turned the sky teal and the cream stone orange), a hair cooler than neutral, shadows a
    /// hair cooler still, a broad soft bloom, a light vignette. No warm tint (JY 11-24: the map and the pieces had gone
    /// yellowish; a warm sun, a warm grade and warm highlights stacked up, as the render lab found on 11-21). The
    /// scene's light (GameSky) is set for this exposure.
    /// Numbers are fields so a tool can try others.
    /// </summary>
    public static class GameGrade
    {
        public static bool On = true;
        public static TonemappingMode Tone = TonemappingMode.Neutral;
        public static float Exposure = -.15f, Contrast = 30f, Saturation = 18f, Temperature = -2f, Tint = 0f;
        public static float BloomThreshold = 1f, BloomIntensity = .45f, BloomScatter = .65f, Vignette = .12f;
        public static Color ShadowTone = new Color(.5f, .5f, .52f), HighlightTone = new Color(.5f, .5f, .5f);
        /// <summary>The light shafts (GameShafts): how bright, how thick the air, how much they lean toward the sun, how
        /// far they are marched (m), their tint over the sun's. The render lab's hall had 1.6, .03, .35, 90.</summary>
        public static float ShaftIntensity = .6f, ShaftDensity = .015f, ShaftAnisotropy = .4f, ShaftDistance = 90f;
        public static Color ShaftTint = new Color(1f, .97f, .93f);
        public const float Priority = 20f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            On = true;
            graded = false;
            volumes.Clear();
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
        }

        static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single && SceneManager.GetActiveScene() != scene) return;
            // Only the round's scenes (GameGfx.InScope); the others keep GameLook's grade.
            if (!GameGfx.InScope(scene))
            {
                if (mode == LoadSceneMode.Single) graded = false;
                GameLook.RefreshColourAid();
                return;
            }
            var go = new GameObject("Game grade (JY-gfx)");
            SceneManager.MoveGameObjectToScene(go, scene);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = Priority;
            // One profile for every scene (Profile() makes a new one, for a tool trying other numbers).
            if (shared == null) shared = Profile();
            volume.sharedProfile = shared;
            volume.enabled = On;
            volumes.RemoveAll(v => v == null);
            volumes.Add(volume);
            graded = true;
            GameLook.RefreshColourAid();
        }

        static bool graded;

        /// <summary>True while this grade's tonemapper (not ACES) is the one on screen. The colour aid (GameLook, priority
        /// 1001) must then mix its channels in linear sRGB, the space URP grades in without ACES, not in ACEScg.</summary>
        public static bool ReplacesAces => On && graded && Tone != TonemappingMode.ACES;

        static VolumeProfile shared;
        static readonly System.Collections.Generic.List<Volume> volumes = new System.Collections.Generic.List<Volume>();

        /// <summary>Turns the grade on or off in the scenes now loaded and the ones to come (GameGfx).</summary>
        public static void Enable(bool on)
        {
            On = on;
            volumes.RemoveAll(v => v == null);
            foreach (var v in volumes) v.enabled = on;
            GameLook.RefreshColourAid();
        }

        public static VolumeProfile Profile()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Game grade (JY-gfx)";
            profile.Add<Tonemapping>(false).mode.Override(Tone);
            var adjust = profile.Add<ColorAdjustments>(false);
            adjust.postExposure.Override(Exposure);
            adjust.contrast.Override(Contrast);
            adjust.saturation.Override(Saturation);
            var balance = profile.Add<WhiteBalance>(false);
            balance.temperature.Override(Temperature);
            balance.tint.Override(Tint);
            var split = profile.Add<SplitToning>(false);
            split.shadows.Override(ShadowTone);
            split.highlights.Override(HighlightTone);
            split.balance.Override(0f);
            var bloom = profile.Add<Bloom>(false);
            bloom.threshold.Override(BloomThreshold);
            bloom.intensity.Override(BloomIntensity);
            bloom.scatter.Override(BloomScatter);
            bloom.highQualityFiltering.Override(true);
            profile.Add<Vignette>(false).intensity.Override(Vignette);
            var shafts = profile.Add<GfxLightShafts>(false);
            shafts.intensity.Override(ShaftIntensity);
            shafts.density.Override(ShaftDensity);
            shafts.anisotropy.Override(ShaftAnisotropy);
            shafts.maxDistance.Override(ShaftDistance);
            shafts.tint.Override(ShaftTint);
            return profile;
        }
    }
}
#endif
