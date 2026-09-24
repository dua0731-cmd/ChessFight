using System;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // The one clock every obstacle reads. Offline it is plain scene time; in a
    // match the network layer swaps in a clock that all clients share, so every
    // player sees each obstacle in the same place without a single packet being
    // sent about it.
    public static class ObstacleClock
    {
        static Func<double> source;

        public static double Now => source != null ? source() : Time.timeAsDouble;
        public static bool Shared => source != null;

        // null restores scene time.
        public static void Use(Func<double> clock) => source = clock;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => source = null;
    }
}
