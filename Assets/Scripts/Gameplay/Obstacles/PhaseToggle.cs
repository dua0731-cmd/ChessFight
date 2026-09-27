using UnityEngine;

namespace ChessFight.Gameplay
{
    // Something that comes and goes on a beat: the stair of light of the Queen of the
    // Hill's seventh floor. Solid and shown while the shared clock is between
    // `onFrom` and `onTo` of each period (fractions 0..1), gone the rest of the time.
    // A pure function of the clock (G2), so every PC agrees without a packet.
    //
    // It switches this object's colliders and renderers, not the object itself, so
    // it keeps running while "gone".
    public sealed class PhaseToggle : MonoBehaviour
    {
        [SerializeField] float period = 4f;
        [SerializeField, Range(0f, 1f)] float onFrom;
        [SerializeField, Range(0f, 1f)] float onTo = 0.7f;
        [Tooltip("Seconds added to the shared clock, so steps side by side come and go in a wave.")]
        [SerializeField] float phase;

        Collider[] colliders;
        Renderer[] renderers;
        bool shown = true;

        public bool Solid => shown;

        public void Configure(float period, float onFrom, float onTo, float phase)
        {
            this.period = period;
            this.onFrom = onFrom;
            this.onTo = onTo;
            this.phase = phase;
        }

        // Is it solid at this time? Tools/QueenHill/preview/index.html mirrors this.
        public static bool On(double time, float period, float onFrom, float onTo, float phase)
        {
            if (period <= 1e-3f) return true;
            double u = (time + phase) / period;
            u -= System.Math.Floor(u);
            return u >= onFrom && u < onTo;
        }

        void Awake()
        {
            colliders = GetComponentsInChildren<Collider>(true);
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        void FixedUpdate() => Apply();
        void Update() => Apply();

        void Apply()
        {
            bool on = On(ObstacleClock.Now, period, onFrom, onTo, phase);
            if (on == shown) return;
            shown = on;
            foreach (var c in colliders) if (c != null) c.enabled = on;
            foreach (var r in renderers) if (r != null) r.enabled = on;
        }
    }
}
