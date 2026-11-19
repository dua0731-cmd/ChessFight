using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The Royal Observatory's iris gate (R720, obstacle plan v0.1 #7): one of the two leaves that close a ring of the
    // great telescope's tube from left and right (the plan's eight blades are for the art; the plan itself allows
    // two walls for what is collided with). Its cycle:
    //   open -> warn (it trembles and its warning shows) -> close -> shut -> reopen
    // all a pure function of the shared obstacle clock, as every obstacle is. It slides along its own forward (+z)
    // by `travel`; what it meets it pushes, by contact - its leading edge is a prow, so a pawn caught between the
    // two leaves is squeezed out up or down the deck, not crushed.
    // (It takes the place of R712's TelescopeRam, which had the same kind of cycle and no use in structure v0.3.)
    public sealed class IrisLeaf : Obstacle
    {
        [SerializeField] float travel = 2.5f;
        [SerializeField] float open = 2.4f, warn = .8f, close = .5f, shut = .8f, reopen = .5f;
        [Tooltip("Shown from the warning until the leaf is open again.")]
        [SerializeField] GameObject warning;

        const float Tremble = .02f;   // of the travel, while it warns

        public float Cycle => open + warn + close + shut + reopen;

        // Call it on an inactive object (the builder's root is switched on at the end).
        public void Configure(float travel, float open, float warn, float close, float shut, float reopen, GameObject warning, float phaseSeconds)
        {
            this.travel = travel;
            this.open = Mathf.Max(0f, open);
            this.warn = Mathf.Max(0f, warn);
            this.close = Mathf.Max(.05f, close);
            this.shut = Mathf.Max(0f, shut);
            this.reopen = Mathf.Max(.05f, reopen);
            this.warning = warning;
            SetPhase(phaseSeconds);
        }

        float CycleTime(double time)
        {
            double cycle = Cycle;
            double t = time % cycle;
            return (float)(t < 0 ? t + cycle : t);
        }

        // How far it has closed at a time: 0 open, 1 shut.
        public float Closed(double time)
        {
            float t = CycleTime(time);
            if (t < open) return 0f;
            t -= open;
            // A tremble toward the way, nothing at either end of the warning.
            if (t < warn) return warn > 0f ? Tremble * Mathf.Abs(Mathf.Sin(t * 25f)) * Mathf.Sin(t / warn * Mathf.PI) : 0f;
            t -= warn;
            // Straight in at one speed: travel / close (2.6 m in 0.5 s is 5.2 m/s).
            if (t < close) return t / close;
            t -= close;
            if (t < shut) return 1f;
            t -= shut;
            return 1f - Mathf.SmoothStep(0f, 1f, t / reopen);
        }

        // True from the warning until it is open again.
        public bool Warning(double time) => CycleTime(time) >= open;

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            rotation = StartRotation;
            position = StartPosition + StartRotation * Vector3.forward * (travel * Closed(time));
        }

        void Update()
        {
            if (warning == null) return;
            bool on = Warning(ObstacleClock.Now + phase);
            if (warning.activeSelf != on) warning.SetActive(on);
        }
    }
}
