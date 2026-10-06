using System;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // What a mini-game in a course slot gives the slot (Pawn Rush mini-game spec v0.1:
    // MiniGameBase): progress 0..1 and one "done" signal. The slot opens its door on
    // that signal and nothing else; how the game is played is the game's own business.
    // Only the placeholder exists so far (MiniGamePlaceholder).
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

        // A new round: back to the start.
        public virtual void ResetGame()
        {
            Completed = false;
            Progress01 = 0f;
        }
    }
}
