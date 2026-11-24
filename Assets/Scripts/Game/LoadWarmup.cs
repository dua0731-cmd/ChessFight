using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Game
{
    // R732: work a match scene does behind the loading screen before this PC may call itself ready - the Pawn Rush
    // round's pre-draw (PawnRush RoundPrewarm). The match loader keeps the screen up, and this PC's load short of
    // ready, while anything holds it; each holder says how far it has got (0 to 1). A holder that is destroyed without
    // letting go (its scene went) counts no more.
    public static class LoadWarmup
    {
        static readonly Dictionary<object, float> held = new Dictionary<object, float>();
        static readonly List<object> gone = new List<object>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => held.Clear();

        public static bool Busy
        {
            get
            {
                Prune();
                return held.Count > 0;
            }
        }

        // How far the slowest holder has got; 1 with none.
        public static float Fraction
        {
            get
            {
                Prune();
                float least = 1f;
                foreach (var f in held.Values) least = Mathf.Min(least, f);
                return least;
            }
        }

        public static void Hold(object holder, float fraction = 0f)
        {
            if (holder != null) held[holder] = Mathf.Clamp01(fraction);
        }

        public static void Release(object holder)
        {
            if (holder != null) held.Remove(holder);
        }

        static void Prune()
        {
            gone.Clear();
            foreach (var key in held.Keys)
                if (key is Object unityObject && unityObject == null) gone.Add(key);
            foreach (var key in gone) held.Remove(key);
        }
    }
}
