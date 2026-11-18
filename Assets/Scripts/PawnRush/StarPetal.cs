using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The Royal Observatory's variable star (R720, obstacle plan v0.1 #1): one of the five petals round a star
    // platform's core. The petals fold down one after another and come back up together:
    //   level -> warn (its warning blinks) -> fold (each `stagger` after the one before it) -> hang -> all come up
    // The petal's origin is its hinge - its edge by the core, along its own x - and its +z points out from the star,
    // so it folds by turning about x. A pure function of the shared obstacle clock, as every obstacle is: whoever
    // stands on it rides it down and slides off.
    public sealed class StarPetal : Obstacle
    {
        [Tooltip("0 folds first.")]
        [SerializeField] int order;
        [SerializeField] float level = 2.6f, warn = .8f, stagger = .25f, fold = .5f, rise = .75f, cycle = 6f;
        [Tooltip("Degrees it hangs down when folded.")]
        [SerializeField] float angle = 84f;
        [Tooltip("Blinks from `warn` seconds before this petal folds.")]
        [SerializeField] GameObject warning;

        public float Cycle => cycle;
        // When in the cycle this petal starts to fold, and when they all start to come up.
        public float FoldsAt => level + warn + stagger * order;
        public float RisesAt => cycle - rise;

        // Call it on an inactive object (the builder's root is switched on at the end).
        public void Configure(int order, float level, float warn, float stagger, float fold, float rise, float cycle, float angle,
                              GameObject warning, float phaseSeconds)
        {
            this.order = Mathf.Max(0, order);
            this.level = Mathf.Max(0f, level);
            this.warn = Mathf.Max(0f, warn);
            this.stagger = Mathf.Max(0f, stagger);
            this.fold = Mathf.Max(.05f, fold);
            this.rise = Mathf.Max(.05f, rise);
            this.cycle = Mathf.Max(this.level + this.warn + this.fold + this.rise, cycle);
            this.angle = angle;
            this.warning = warning;
            SetPhase(phaseSeconds);
        }

        float CycleTime(double time)
        {
            double c = cycle;
            double t = time % c;
            return (float)(t < 0 ? t + c : t);
        }

        // 0 level, 1 folded down.
        public float Folded(double time)
        {
            float t = CycleTime(time);
            if (t < FoldsAt) return 0f;
            // It drops like something let go: slowly, then fast.
            float down = Mathf.Clamp01((Mathf.Min(t, RisesAt) - FoldsAt) / fold);
            down *= down;
            return t < RisesAt ? down : down * (1f - Mathf.SmoothStep(0f, 1f, (t - RisesAt) / rise));
        }

        // True for the `warn` seconds before it folds.
        public bool Warning(double time)
        {
            float t = CycleTime(time);
            return t >= FoldsAt - warn && t < FoldsAt;
        }

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            position = StartPosition;
            rotation = StartRotation * Quaternion.AngleAxis(angle * Folded(time), Vector3.right);
        }

        void Update()
        {
            if (warning == null) return;
            double now = ObstacleClock.Now + phase;
            // Four blinks a second while it warns.
            bool on = Warning(now) && (long)System.Math.Floor(now * 8.0) % 2 == 0;
            if (warning.activeSelf != on) warning.SetActive(on);
        }
    }
}
