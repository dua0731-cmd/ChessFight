using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A block of the royal clock tower's ratchet wall (R781): it slides out of the wall, waits to be stepped on, lifts
    // whoever stands on it one row up, holds there, slides back in and goes down again inside the wall - a clock's
    // escapement drawn as a staircase that only goes up. A pure function of the shared obstacle clock (HANDOFF rule 7), so
    // every PC sees the same block in the same place without a packet. Riders stand on it through IMovingSurface.
    //
    // One cycle of `period` seconds, from where it was placed (inside the wall, at its low height):
    //   out (outTime) -> wait low (waitLow) -> up `rise` (riseTime, eased) -> wait high (waitHigh) -> in (outTime) -> down inside
    public sealed class RatchetStep : Obstacle
    {
        [SerializeField] Vector3 outward = new Vector3(0f, 0f, -.9f);
        [SerializeField] float rise = 2.5f;
        [SerializeField] float period = 8f, outTime = .3f, waitLow = 1.5f, riseTime = 2f, waitHigh = 1.2f;

        public float Period => period;
        public float Rise => rise;

        // Call on an INACTIVE object, before Awake evaluates the first pose.
        public void Configure(Vector3 outwardLocal, float riseMetres, float periodSeconds, float delay)
        {
            outward = outwardLocal;
            rise = riseMetres;
            period = periodSeconds;
            SetPhase(-delay);
        }

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            rotation = StartRotation;
            double u = time % period;
            if (u < 0.0) u += period;
            float t = (float)u, o, h;
            float a = outTime, b = a + waitLow, c = b + riseTime, d = c + waitHigh, e = d + outTime;
            if (t < a) { o = Smooth(t / a); h = 0f; }
            else if (t < b) { o = 1f; h = 0f; }
            else if (t < c) { o = 1f; h = Smooth((t - b) / riseTime); }
            else if (t < d) { o = 1f; h = 1f; }
            else if (t < e) { o = 1f - Smooth((t - d) / outTime); h = 1f; }
            else { o = 0f; h = 1f - Smooth((t - e) / Mathf.Max(.01f, period - e)); }
            position = StartPosition + StartRotation * (outward * o + Vector3.up * (rise * h));
        }

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
