using ChessFight.Game;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // R514 (the user, 11-09: "인게임에서 스테미너 표시 안됨 버그"): this PC's stamina in a match, the same bar the offline
    // playtest shows (StaminaBarGui). Only the playtest drew it, and a match switches the playtest off
    // (PlaytestSpawner.NetworkDriven), so no match ever showed it. The match link hands this its own pawn (Follow) and
    // switches it on once the race is under way, after the loading screen; on a client the value is the host's, which
    // every snapshot carries for each pawn (RagdollWire's stamina byte).
    public sealed class MatchStaminaBar : MonoBehaviour
    {
        Component source;   // an IStaminaReadout (the pawn's driver); a Component so a destroyed one reads as null
        GUIStyle style;

        /// <summary>The pawn to show (its driver). Anything without a stamina readout shows nothing.</summary>
        public void Follow(Component driver) => source = driver is IStaminaReadout ? driver : null;

        void OnGUI()
        {
            if (source == null || !(source is IStaminaReadout readout)) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { font = RuntimePanels.KoreanFont, fontSize = 14, richText = true };
            StaminaBarGui.Draw(readout.Stamina01, style);
        }
    }
}
