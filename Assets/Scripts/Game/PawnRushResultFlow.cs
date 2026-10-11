using System;
using ChessFight.Network;
using UnityEngine.SceneManagement;

namespace ChessFight.Game
{
    // From the end of a Pawn Rush race to the result scenes and back to the lobby (R125).
    //
    // The course (offline playtest, or the online match link) decides the race and calls Show: the winners'
    // PCs load PawnRushVictory, the losers' PawnRushLose, and the result scene takes the match from here
    // (PawnRushResultDirector). When the result is over (its 12 s, or a button) it calls ToLobby: online the
    // match link has set Leave, which leaves the match room, and the runtime loads the lobby as it does for any
    // match that ends; offline the lobby scene is loaded directly.
    public static class PawnRushResultFlow
    {
        static MatchResult pending;
        static int pendingTeam;
        static bool leaving;

        // Online: leave the match (set by the match link; the lobby follows from the session).
        public static Action Leave;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            pending = null;
            pendingTeam = 0;
            leaving = false;
            Leave = null;
        }

        // Load this PC's result scene for `result`, seen by a player of `team`.
        public static void Show(MatchResult result, int team)
        {
            pending = result;
            pendingTeam = team == 1 ? 1 : 0;
            leaving = false;
            bool won = result != null && result.WinningTeam == pendingTeam;
            SceneManager.LoadScene(won ? SceneNames.PawnRushVictory : SceneNames.PawnRushLose);
        }

        // The result scene asks for the match it is to show (false: opened on its own, a preview).
        public static bool Take(out MatchResult result, out int team)
        {
            result = pending;
            team = pendingTeam;
            pending = null;
            return result != null;
        }

        // Back to the lobby, once.
        public static void ToLobby()
        {
            if (leaving) return;
            leaving = true;
            var leave = Leave;
            Leave = null;
            if (leave != null)
            {
                try { leave(); return; }
                catch (Exception e) { UnityEngine.Debug.LogException(e); }
            }
            SceneManager.LoadScene(SceneNames.Lobby);
        }
    }
}
