using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The Royal Observatory's telescope ram (R712, level design v0.1 beat 6): a block that waits drawn back beside the
    // way, warns, thrusts across it, holds there and draws back. The imported 03 pusher is a plain sine (no wait, no
    // warning: of its travel it is out of the way only a quarter of its cycle), so this one has a cycle of its own:
    //   rest -> warn (it draws back a hand's breadth and its warning shows) -> thrust -> hold -> back
    // all a pure function of the shared obstacle clock, as every obstacle is. It moves along its own forward (+z) by
    // `travel`; what it meets it pushes, by contact.
    public sealed class TelescopeRam : Obstacle
    {
        [SerializeField] float travel = 6f;
        [SerializeField] float rest = 1.6f, warn = 1f, thrust = .5f, hold = .5f, back = .9f;
        [Tooltip("Shown from the warning until the ram is back.")]
        [SerializeField] GameObject warning;

        const float DrawBack = .04f;   // of the travel, while it warns

        public float Cycle => rest + warn + thrust + hold + back;
        // Seconds of a cycle in which a runner may be in the ram's way: its rest and its warning.
        public float Clear => rest + warn;

        // Call it on an inactive object (the builder's root is switched on at the end).
        public void Configure(float travel, float rest, float warn, float thrust, float hold, float back, GameObject warning, float phaseSeconds)
        {
            this.travel = travel;
            this.rest = Mathf.Max(0f, rest);
            this.warn = Mathf.Max(0f, warn);
            this.thrust = Mathf.Max(.05f, thrust);
            this.hold = Mathf.Max(0f, hold);
            this.back = Mathf.Max(.05f, back);
            this.warning = warning;
            SetPhase(phaseSeconds);
        }

        float CycleTime(double time)
        {
            double cycle = Cycle;
            double t = time % cycle;
            return (float)(t < 0 ? t + cycle : t);
        }

        // How far out it is at a time: 0 drawn back, 1 across the way.
        public float Reach(double time)
        {
            float t = CycleTime(time);
            if (t < rest) return 0f;
            t -= rest;
            if (t < warn) return warn > 0f ? -DrawBack * Mathf.Sin(t / warn * Mathf.PI * .5f) : 0f;
            t -= warn;
            // Straight out at one speed: travel / thrust (6 m in 0.5 s is 12 m/s).
            if (t < thrust) return Mathf.Lerp(-DrawBack, 1f, t / thrust);
            t -= thrust;
            if (t < hold) return 1f;
            t -= hold;
            return 1f - Mathf.SmoothStep(0f, 1f, t / back);
        }

        // True from the warning until it is back.
        public bool Warning(double time) => CycleTime(time) >= rest;

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            rotation = StartRotation;
            position = StartPosition + StartRotation * Vector3.forward * (travel * Reach(time));
        }

        void Update()
        {
            if (warning == null) return;
            bool on = Warning(ObstacleClock.Now + phase);
            if (warning.activeSelf != on) warning.SetActive(on);
        }
    }
}
