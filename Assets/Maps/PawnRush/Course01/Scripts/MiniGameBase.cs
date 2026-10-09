using System;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // What a mini-game gives its mission station (Pawn Rush mini-game spec v0.1: MiniGameBase):
    // progress 0..1 and one "done" signal. The station opens its team gate on that signal and
    // nothing else; how the game is played is the game's own business (MissionGames). Progress
    // is worked out where the characters are simulated (offline: here; online: the host).
    public abstract class MiniGameBase : MonoBehaviour
    {
        public float Progress01 { get; protected set; }
        public bool Completed { get; private set; }

        public event Action Finished;

        protected void Complete()
        {
            if (Completed) return;
            Completed = true;
            Progress01 = 1f;
            Finished?.Invoke();
        }

        // Development shortcut (the playtest's F6): done now.
        public void ForceComplete() => Complete();

        // A new round: back to the start.
        public virtual void ResetGame()
        {
            Completed = false;
            Progress01 = 0f;
        }
    }
}
