using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ChessFight.Game
{
    /// <summary>
    /// JY-gfx (2026-11-24, the graphics upgrade): the sky and the sun of every game scene nobody gave its own.
    /// A scene of the round (GameGfx.InScope) that still has Unity's default procedural sky (the round, the sections
    /// test, the courses) gets the game's sky (Resources/Gfx/GfxSky: real volumetric clouds over a storybook blue, see
    /// Shaders/GfxSky.shader) and a near-white sun; its ambient light and its reflections are then worked out from that sky
    /// (DynamicGI.UpdateEnvironment). Places with their own light (ZoneLight) still mix their own over it, and scenes
    /// that set their own sky (the lobby, the intro, the carriage yard, the observatory) keep theirs.
    /// The air in between: a scene with no fog of its own (or only the round's far fog, PawnRushRound.FarFog) gets a
    /// light haze the colour of the sky's horizon, so far walls and towers stand back from near ones (aerial
    /// perspective). It is the scene's fog, so places with a haze of their own (ZoneLight) mix theirs from it.
    /// </summary>
    public static class GameSky
    {
        /// <summary>The sun: near white (a warm one, with the rest, turned the map and the pieces yellow).</summary>
        public static Color SunColor = new Color(1f, .98f, .96f);
        /// <summary>The way the round's sun shines (world): from ahead and a little to the right, about 37 degrees up,
        /// so the course's cameras (looking along +z) look into the light, as the render lab did for the hall (JY-look):
        /// beams, shadows and the shafts come toward the player, and the haze glows. Resources/Gfx/Sky/sky.exr was
        /// rendered with this sun (ArtSource/Gfx/sky.py --sun-dir -0.501,-0.622,-0.602, Blender's axes).</summary>
        public static Vector3 SunWay = new Vector3(-.5f, -.6f, -.62f);
        public static float SunIntensity = 1.65f;
        /// <summary>The sky's brightness in view and as the scene's ambient light.</summary>
        public static float SkyExposure = .9f, AmbientIntensity = .8f;
        /// <summary>The haze: linear from HazeStart to HazeEnd metres, the horizon's colour.</summary>
        public static bool HazeOn = true;
        public static float HazeStart = 60f, HazeEnd = 1400f;
        public static Color HazeColor = new Color(.72f, .8f, .9f);

        const string DefaultSkyName = "Default-Skybox";
        static Material sky;

        /// <summary>Whether scenes get the game's sky, sun and haze (GameGfx turns it off to compare).</summary>
        public static bool On = true;

        // What Apply changed in the active scene, to put back (Enable(false)).
        static Material sky0;
        static Light sun0;
        static Color sunColour0;
        static Quaternion sunRotation0;
        static float sunIntensity0, ambientIntensity0;
        static LightShadows sunShadows0;
        static AmbientMode ambientMode0;
        static DefaultReflectionMode reflection0;
        static bool fog0, saved;
        static FogMode fogMode0;
        static Color fogColour0;
        static float fogStart0, fogEnd0;

        /// <summary>Puts the game's sky, sun and haze in the active scene (on), or what it had before (off).</summary>
        public static void Enable(bool on)
        {
            On = on;
            if (on)
            {
                Apply();
                return;
            }
            if (!saved) return;
            saved = false;
            RenderSettings.skybox = sky0;
            if (sun0 != null)
            {
                sun0.color = sunColour0;
                sun0.transform.rotation = sunRotation0;
                sun0.intensity = sunIntensity0;
                sun0.shadows = sunShadows0;
            }
            RenderSettings.ambientMode = ambientMode0;
            RenderSettings.ambientIntensity = ambientIntensity0;
            RenderSettings.defaultReflectionMode = reflection0;
            RenderSettings.fog = fog0;
            RenderSettings.fogMode = fogMode0;
            RenderSettings.fogColor = fogColour0;
            RenderSettings.fogStartDistance = fogStart0;
            RenderSettings.fogEndDistance = fogEnd0;
            DynamicGI.UpdateEnvironment();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            On = true;
            saved = false;
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
            // Places that note the scene's sky when they come in and put it back when they go (the carriage yard,
            // the observatory) may have noted the default one: a watcher swaps it again whenever it comes back.
            var go = new GameObject("Game sky") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Watch>();
        }

        sealed class Watch : MonoBehaviour
        {
            float next;

            void Update()
            {
                if (!On || Time.unscaledTime < next) return;
                next = Time.unscaledTime + .5f;
                var current = RenderSettings.skybox;
                if (current != null && current.name == DefaultSkyName) Apply();
            }
        }

        static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single && SceneManager.GetActiveScene() != scene) return;
            if (mode == LoadSceneMode.Single) saved = false;
            if (On) Apply();
        }

        /// <summary>Puts the game's sky in the active scene if it still has the default one; true if it did.</summary>
        public static bool Apply()
        {
            var current = RenderSettings.skybox;
            // Only the round's scenes (GameGfx.InScope), and only Unity's default sky (a scene with no sky or its own
            // keeps it: Sword Fight's study, the lobby, the tool scenes light themselves).
            if (!On || !GameGfx.InScope() || current == null || (current.name != DefaultSkyName && current != sky)) return false;
            var sun = RenderSettings.sun != null ? RenderSettings.sun : FindSun();
            if (current != sky)
            {
                // The scene as it was, to put back.
                saved = true;
                sky0 = current;
                sun0 = sun;
                if (sun != null)
                {
                    sunColour0 = sun.color;
                    sunRotation0 = sun.transform.rotation;
                    sunIntensity0 = sun.intensity;
                    sunShadows0 = sun.shadows;
                }
                ambientMode0 = RenderSettings.ambientMode;
                ambientIntensity0 = RenderSettings.ambientIntensity;
                reflection0 = RenderSettings.defaultReflectionMode;
                fog0 = RenderSettings.fog;
                fogMode0 = RenderSettings.fogMode;
                fogColour0 = RenderSettings.fogColor;
                fogStart0 = RenderSettings.fogStartDistance;
                fogEnd0 = RenderSettings.fogEndDistance;
            }
            if (sky == null)
            {
                var source = Resources.Load<Material>("Gfx/GfxSky");
                if (source == null) return false;
                sky = new Material(source) { name = "Gfx sky" };
            }
            sky.SetFloat("_Exposure", SkyExposure);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = AmbientIntensity;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            // Only the plain white default sun: a scene that chose its own colour keeps it.
            if (sun != null && sun.color.r > .99f && sun.color.g > .99f && sun.color.b > .99f)
            {
                sun.color = SunColor;
                sun.intensity = SunIntensity;
                sun.transform.rotation = Quaternion.LookRotation(SunWay.normalized);
                sun.shadows = LightShadows.Soft;
            }
            // The haze, unless the scene has a fog of its own (the far fog is no fog: 3000 m and on).
            if (HazeOn && (!RenderSettings.fog || (RenderSettings.fogMode == FogMode.Linear && RenderSettings.fogStartDistance >= 2000f)))
            {
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = HazeStart;
                RenderSettings.fogEndDistance = HazeEnd;
                RenderSettings.fogColor = HazeColor;
                RenderSettings.fog = true;
            }
            DynamicGI.UpdateEnvironment();
            return true;
        }

        static Light FindSun()
        {
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional && light.isActiveAndEnabled) return light;
            return null;
        }
    }
}
