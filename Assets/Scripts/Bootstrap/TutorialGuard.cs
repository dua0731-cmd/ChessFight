using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.Game
{
    // R841: the first-run tutorial must never stand between a player and friends (Docs/Player/TUTORIAL_DESIGN_v0.1.md
    // 7: "튜토리얼 중 친구 초대를 수락하면: 건너뛴 것으로 치고 로비로"). NetworkRuntime puts this in the Tutorial scene: once
    // the player is with someone - an invite accepted in the Steam overlay, a friend who joined this player's party,
    // a room entered - the tutorial counts as skipped and the lobby loads, where the party and the room are shown.
    // Alone (the usual case: a party of one) it does nothing. Offline it does nothing.
    public sealed class TutorialGuard : MonoBehaviour
    {
        bool leaving;

        /// <summary>This player is with others: more than one in its party, or in a match room.</summary>
        public static bool WithOthers()
        {
            var session = NetworkRuntime.Instance != null ? NetworkRuntime.Instance.Session : null;
            if (session == null || !session.Online) return false;
            return session.Match != 0 || session.PartyMembers.Length > 1;
        }

        void Update()
        {
            if (leaving || !WithOthers()) return;
            leaving = true;
            GameSettings.SetBool(SceneNames.TutorialDone, true);
            Debug.Log("[Tutorial] 다른 플레이어와 함께라 튜토리얼을 건너뛴 것으로 하고 로비로 갑니다.");
            GameMenu.Close();
            SceneTransition.ToLobby(new Vector2(.5f, .5f));
        }
    }
}
