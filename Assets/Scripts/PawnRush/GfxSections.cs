using System.Collections;
using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.PawnRush
{
    /// <summary>
    /// JY-gfx (2026-11-24): the graphics upgrade's look at a round's sections as they come in. A round lays its
    /// sections out under a switched-off holder and switches them on together; this watch finds each switched-on
    /// section once and:
    ///   - hands course 2's and course 3's sections to GfxMapLights (each map's own light: its look and its rig); the
    ///     Royal Library and the Observatory keep their own light (RoyalLibraryLighting, RoyalObservatoryLighting);
    ///   - hands course 1's to GfxGround (tufts and pebbles on the paving);
    ///   - wakes the upgrade's looks over the scene's renderers (GameSurfaces, GameGrass, GameCharacters), which
    ///     otherwise look only every ten seconds.
    /// Courses by the section's name: Early_ course 1 (the castle's outside), Mid_ course 2 (its entrance), Late_ and
    /// Final_ course 3 (its depths and the end).
    /// </summary>
    public static class GfxSections
    {
        static readonly HashSet<int> looked = new HashSet<int>();
        static readonly string[] OwnLight = { "3-1_RoyalLibrary", "3-2_RoyalObservatory" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            looked.Clear();
            var go = new GameObject("Gfx sections (JY-gfx)") { hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Watch>();
        }

        /// <summary>The course a section belongs to by its name: 1, 2, 3, or 0 for none.</summary>
        public static int CourseOf(string name)
        {
            if (name.Contains("Early_")) return 1;
            if (name.Contains("Mid_")) return 2;
            if (name.Contains("Late_") || name.Contains("Final_")) return 3;
            return 0;
        }

        /// <summary>Every section switched on and not looked at yet.</summary>
        public static void Find()
        {
            if (!GameGfx.InScope()) return;
            bool fresh = false;
            foreach (var section in Object.FindObjectsByType<PawnRushSection>(FindObjectsSortMode.None))
            {
                if (!looked.Add(section.GetInstanceID())) continue;
                fresh = true;
                int course = CourseOf(section.name);
                if (course == 1) GfxGround.Request(section);
                bool own = false;
                foreach (var n in OwnLight) own |= section.name.Contains(n);
                if ((course == 2 || course == 3) && !own) GfxMapLights.Dress(section, course);
            }
            if (!fresh) return;
            GameSurfaces.Wake();
            GameGrass.Wake();
            GameCharacters.Wake();
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
