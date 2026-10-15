using System;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // The one clock every obstacle reads. Offline it is plain scene time; in a
    // match the network layer swaps in a clock that all clients share, so every
    // player sees each obstacle in the same place without a single packet being
    // sent about it.
    //
    // R126: Authority says whether this PC is the one whose physics counts - offline
    // always, in a match only the host. Obstacles that push, throw or carry characters
    // (the imported hammers, falling pieces, vents, jump pads, conveyors) act only where
    // it is set. Before R126 they read "the clock is not shared" instead, which switched
    // them off on the host too: online they were drawn swinging and hitting, and nothing
    // happened to anyone.
    public static class ObstacleClock
    {
        static Func<double> source;

        public static double Now => source != null ? source() : Time.timeAsDouble;
        public static bool Shared => source != null;
        // The clock in use (null: scene time), so a match link can fall back on it.
        public static Func<double> Source => source;
        // This PC simulates the characters: offline, or the host of a match.
        public static bool Authority = true;

        // null restores scene time.
        public static void Use(Func<double> clock) => source = clock;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            source = null;
            Authority = true;
        }
    }
}
