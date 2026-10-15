using System;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // What a mini-game gives its mission station (Pawn Rush mini-game spec v0.1: MiniGameBase):
    // progress 0..1 and one "done" signal. The station opens its team gate on that signal and
    // nothing else; how the game is played is the game's own business (MissionGames). Progress
    // is worked out where the characters are simulated (offline: here; online: the host).
    //
    // Online (R92, R126): only the host plays the games out (Remote is set everywhere else). With every
    // snapshot the host sends each station's progress, done, and a block the game writes itself (WriteNet,
    // up to PawnRushNetState.BlockBytes); the other PCs read it (ReadNet) when the pawns they draw reach the
    // moment it was taken, so the game's things move in step with the bodies. The rules the spec calls for
    // (mini-games v1.0 §1.2):
    //   - anything a pawn touches is kinematic and moved by the host's numbers, never by physics on a client;
    //   - things that move on time alone are a function of the shared obstacle clock (no bytes at all);
    //   - a client never judges; it draws, and plays the effects of what changed in the block itself.
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

        public static bool Remote;

        // A pawn's roster place and back (online: set by the match link; null offline). A game that sends who
        // does something (who carries a plank) sends this byte.
        public static Func<ICharacterDriver, byte> SeatOf;
        public static Func<byte, ICharacterDriver> DriverOf;
        // The pawn of the person at this PC (what only they see: J's aiming arc). Set by the course's playtest.
        public static ICharacterDriver LocalDriver;
        public const byte NoSeat = 255;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Remote = false;
            SeatOf = null;
            DriverOf = null;
            LocalDriver = null;
        }

        protected static byte Seat(ICharacterDriver driver) => driver != null && SeatOf != null ? SeatOf(driver) : NoSeat;
        protected static ICharacterDriver Driver(byte seat) => seat != NoSeat && DriverOf != null ? DriverOf(seat) : null;

        // Host: what the other PCs need to draw this game beyond its progress.
        public virtual void WriteNet(ref NetBlock block) { }

        // Not the host: the host's block (ReadNet) and its progress and done (ApplyRemote, after it).
        public virtual void ReadNet(ref NetBlock block) { }

        public virtual void ApplyRemote(float progress, bool completed)
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

        // A client eases a kinematic thing toward the host's number instead of jumping 30 times a second.
        protected static float Ease(float shown, float target, float rate = 18f) =>
            Mathf.Lerp(shown, target, 1f - Mathf.Exp(-rate * Time.deltaTime));

        protected static float EaseAngle(float shown, float target, float rate = 18f) =>
            Mathf.LerpAngle(shown, target, 1f - Mathf.Exp(-rate * Time.deltaTime));
    }
}
