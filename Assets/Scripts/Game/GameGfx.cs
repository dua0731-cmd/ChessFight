using UnityEngine;

namespace ChessFight.Game
{
    /// <summary>
    /// JY-gfx (2026-11-24, the graphics upgrade): the whole upgrade as one switch, to see what it does. In the editor,
    /// ` (back quote) in Play turns it off and on again: off is the scene's own sky, sun and fog (GameSky), no grade
    /// volume (GameGrade), no surface sets and plain URP Lit materials (GameSurfaces), no grass (GameGrass); a small
    /// label says so. The renderer asset's longer shadows stay either way. Players never see the switch.
    /// </summary>
    public static class GameGfx
    {
        public static bool Upgrade { get; private set; } = true;

        /// <summary>Turns the upgrade on or off (every part, every time it is called).</summary>
        public static void Set(bool on)
        {
            Upgrade = on;
            GameGrade.Enable(on);
            GameSky.Enable(on);
            GameSurfaces.Enable(on);
            GameGrass.Show(on);
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
