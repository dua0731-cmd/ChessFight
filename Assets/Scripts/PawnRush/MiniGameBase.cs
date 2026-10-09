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

        // Online (R92): only the host plays the games out. On every other PC this is set and the games
        // skip their own rules and show what the host sends (ApplyRemote): the pawns there are puppets
        // drawn a little behind, so working it out again would disagree with the host.
        public static bool Remote;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Remote = false;

        // What a game shows beyond its progress, packed for the host's course state: painted squares,
        // laid planks and piles, rung bells, the capstan's angle. 0 for a game shown by progress alone.
        public virtual ulong StateBits => 0;

        // The host's word on this game (online, not the host).
        public virtual void ApplyRemote(float progress, bool completed, ulong bits)
        {
            Progress01 = Mathf.Clamp01(progress);
            if (completed) Complete();
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
