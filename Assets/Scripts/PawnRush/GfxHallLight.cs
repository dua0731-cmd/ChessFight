using System.Collections;
using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.PawnRush
{
    /// <summary>
    /// JY-gfx (2026-11-24): the palace halls' own light under the upgraded sky. The mid mini-game halls are
    /// MiniGamePalaceArt's shell: walls, wooden roof beams and chandeliers, open to the sky between the beams (the repair
    /// wing, the library, the ballroom, the laundry). The cloud sky's light (GameSky) poured into them, turned their navy
    /// floors royal blue and their white floor tiles lavender, where the default sky had left them dim. (The late halls,
    /// skylit white marble, keep the sky's light: it suits them, and the indoor light only greyed them.) As
    /// the Royal Library brings its own (RoyalLibraryLighting), each hall now brings an indoor ambient light while the
    /// camera is in it: still the sky's cool light, but about as dim as the default sky left the halls and far less
    /// blue. It goes through the zone light mixer (ZoneLight, R734), so every join into and out of a hall fades. The sun
    /// stays the round's. Only while the upgrade's sky is on (GameGfx, GameSky).
    /// </summary>
    public sealed class GfxHallLight : MonoBehaviour
    {
        public static bool On = true;
        /// <summary>The halls' ambient light from above, from the sides and from below (sRGB, as the gradient mode takes
        /// them): still the sky's cool daylight, but less blue and a little dimmer than the cloud sky's (which lit the
        /// navy floors royal blue). Tried: a warm gradient (as the Royal Library's) turned the white halls sepia under
        /// the near-white sun; a neutral one as dim as the default sky's turned the navy floors black.</summary>
        public static Color Sky = new Color(.51f, .59f, .73f), Equator = new Color(.5f, .53f, .61f), Ground = new Color(.32f, .31f, .3f);
        /// <summary>All three colours times this (to compare brighter and dimmer halls).</summary>
        public static float Scale = 1f;
        /// <summary>Metres out of the hall's walls over which its light fades (then eased over about a second).</summary>
        public static float Blend = 10f;

        static readonly HashSet<int> looked = new HashSet<int>();
        ZoneLight zone;
        Transform frame;
        Bounds area;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            looked.Clear();
            var go = new GameObject("Gfx hall light (JY-gfx)") { hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable };
            DontDestroyOnLoad(go);
            go.AddComponent<Watch>();
        }

        /// <summary>Every section switched on and not looked at yet: a palace hall gets its light (its shell's ceiling
        /// group says it is one). New sections also wake the upgrade's looks over the scene's renderers (GameSurfaces,
        /// GameGrass, GameCharacters), which otherwise look only every ten seconds.</summary>
        public static void Find()
        {
            if (!GameGfx.InScope()) return;
            bool fresh = false;
            // Only switched-on sections: a round lays its sections out under a switched-off holder and switches them on
            // together once they are placed, and they dress themselves then.
            foreach (var section in FindObjectsByType<PawnRushSection>(FindObjectsSortMode.None))
            {
                if (!looked.Add(section.GetInstanceID())) continue;
                fresh = true;
                // The small things on its paving too (GfxGround, over a few frames).
                GfxGround.Request(section);
                if (section.GetComponent<GfxHallLight>() != null) continue;
                Transform ceiling = null;
                foreach (var t in section.GetComponentsInChildren<Transform>(true))
                    if (t.name == MiniGamePalaceArt.CeilingName) { ceiling = t; break; }
                if (ceiling == null) continue;
                // A skylit hall (the late halls' marble beams carry glass) keeps the sky's light: under the cloud sky
                // its white marble reads crisp, and the indoor light only greyed it.
                bool skylit = false;
                foreach (var t in ceiling.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Skylight glazing") { skylit = true; break; }
                if (skylit) continue;
                section.gameObject.AddComponent<GfxHallLight>().Take(section.name);
            }
            if (!fresh) return;
            GameSurfaces.Wake();
            GameGrass.Wake();
            GameCharacters.Wake();
        }

        // The hall's box: the section's drawn parts, in the section's own space. (Not the shell's: the halls that bring
        // their own models switch most of the shell off.) Anything bigger than a hall (a backdrop) is left out.
        void Take(string name)
        {
            frame = transform;
            bool any = false;
            var box = new Bounds();
            foreach (var r in GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var b = r.bounds;
                if (b.size.x > 300f || b.size.z > 300f) continue;
                for (int i = 0; i < 8; i++)
                {
                    var corner = frame.InverseTransformPoint(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                    if (!any) { box = new Bounds(corner, Vector3.zero); any = true; }
                    else box.Encapsulate(corner);
                }
            }
            if (!any)
            {
                enabled = false;
                return;
            }
            area = box;
            zone = ZoneLight.Add("Palace hall (JY-gfx): " + name);
        }

        void LateUpdate()
        {
            if (zone == null) return;
            bool wanted = On && GameGfx.Upgrade && GameSky.On && GameGfx.InScope();
            // The round's sun as GameSky sets it: the hall changes the ambient light only.
            zone.SunColor = GameSky.SunColor;
            zone.SunIntensity = GameSky.SunIntensity;
            zone.ShadowStrength = 1f;
            zone.Sky = Sky * Scale;
            zone.Equator = Equator * Scale;
            zone.Ground = Ground * Scale;
            zone.Ease(wanted ? ZoneLight.Inside(frame, area, Blend) : 0f);
        }

        /// <summary>Every hall's box and how much of its light is in (the picture tool's notes).</summary>
        public static string Describe()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var h in FindObjectsByType<GfxHallLight>(FindObjectsSortMode.None))
                sb.Append($"hall {h.name}: box {h.area.center} size {h.area.size}, light {(h.zone != null ? h.zone.Weight : -1f):0.00}; ");
            return sb.ToString();
        }

        void OnDestroy()
        {
            if (zone != null) zone.Remove();
            zone = null;
        }

        // Sections come in as a round is laid out: look when a scene loads and every two seconds (only the sections, a
        // handful of objects).
        sealed class Watch : MonoBehaviour
        {
            float next;

            void OnEnable() => SceneManager.sceneLoaded += Loaded;
            void OnDisable() => SceneManager.sceneLoaded -= Loaded;

            void Loaded(Scene scene, LoadSceneMode mode)
            {
                if (mode == LoadSceneMode.Single) looked.Clear();
                StartCoroutine(Soon());
            }

            IEnumerator Soon()
            {
                yield return null;
                Find();
            }

            void Update()
            {
                if (Time.realtimeSinceStartup < next) return;
                next = Time.realtimeSinceStartup + 2f;
                Find();
            }
        }
    }
}
