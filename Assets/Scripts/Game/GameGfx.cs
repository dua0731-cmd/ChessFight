using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.Game
{
    /// <summary>
    /// JY-gfx (2026-11-24, the graphics upgrade): the whole upgrade as one switch, to see what it does. In the editor,
    /// ` (back quote) in Play turns it off and on again: off is the scene's own sky, sun and fog (GameSky), no grade
    /// volume (GameGrade), no surface sets and plain URP Lit materials (GameSurfaces), no grass (GameGrass), the pieces
    /// flat and in Look A (GameCharacters), no light shafts (GameShafts) or place probes (GameProbes); a small
    /// label says so. The renderer asset's longer shadows stay either way. Players never see the switch.
    /// The upgrade is for the Pawn Rush round and its sections only (InScope): the lobby, the result halls, Sword Fight
    /// and the tool scenes keep the look they set themselves.
    /// </summary>
    public static class GameGfx
    {
        public static bool Upgrade { get; private set; } = true;

        /// <summary>The scenes the upgrade dresses: the round (its sections, mini-games and ending hall are built in
        /// it), the course prototype, and the sections test scene the picture tool plays.</summary>
        public static bool InScope(Scene scene) => scene.IsValid()
            && (scene.name == SceneNames.PawnRushRound || scene.name == SceneNames.PawnRushCourse01 || scene.name == "PawnRush_Sections");

        public static bool InScope() => InScope(SceneManager.GetActiveScene());

        /// <summary>Turns the upgrade on or off (every part, every time it is called).</summary>
        public static void Set(bool on)
        {
            Upgrade = on;
#if CF_URP
            GameGrade.Enable(on);
#endif
            GameSky.Enable(on);
            GameSurfaces.Enable(on);
            GameGrass.Show(on);
            GameCharacters.Show(on);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            Upgrade = true;
            if (!Application.isEditor) return;
            var go = new GameObject("Gfx switch (JY-gfx)") { hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Switch>();
        }

        sealed class Switch : MonoBehaviour
        {
            GUIStyle style;

            void Update()
            {
                if (Input.GetKeyDown(KeyCode.BackQuote)) Set(!Upgrade);
            }

            void OnGUI()
            {
                if (Upgrade) return;
                if (style == null)
                    style = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = Color.yellow } };
                GUI.Label(new Rect(12f, 8f, 600f, 30f), "JY-gfx OFF  ( ` : on )", style);
            }
        }
    }
}
