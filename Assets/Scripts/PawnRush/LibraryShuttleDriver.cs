using UnityEngine;
using ChessFight.Gameplay;

namespace ChessFight.PawnRush
{
    // Drives the library shuttle's take (one 16 s round trip) from the shared obstacle clock instead of the
    // legacy Animation component (R259: with the component, 승규 님 saw the conveyor not go back and forth).
    // Every physics step it samples the clip at ObstacleClock.Now modulo its length, so it always loops, every
    // PC sees the same pose (G2), and the carriage moves in the step the riders read it. Runs before the
    // ragdoll (-50) so a rider never reads last step's carriage.
    [DefaultExecutionOrder(-200)]
    public sealed class LibraryShuttleDriver : MonoBehaviour
    {
        [SerializeField] AnimationClip clip;
        [SerializeField] Transform carriage;

        double stepKey = double.NaN;
        Vector3 carriageVelocity;

        public Vector3 CarriageVelocity { get { Step(); return carriageVelocity; } }

        public void Configure(AnimationClip take, Transform movingCarriage)
        {
            clip = take;
            carriage = movingCarriage;
        }

        void OnEnable()
        {
            var anim = GetComponent<Animation>();
            if (anim != null) anim.enabled = false;
            stepKey = double.NaN;
            Sample(ObstacleClock.Now);
            carriageVelocity = Vector3.zero;
        }

        void FixedUpdate() => Step();

        // Once per physics step, for whoever asks first (this FixedUpdate or a rider's).
        void Step()
        {
            double key = Time.fixedTimeAsDouble;
            if (key == stepKey || clip == null) return;
            stepKey = key;
            Vector3 before = carriage != null ? carriage.position : Vector3.zero;
            Sample(ObstacleClock.Now);
            carriageVelocity = carriage != null
                ? (carriage.position - before) / Mathf.Max(Time.fixedDeltaTime, 1e-4f)
                : Vector3.zero;
            // The loop's seam is continuous (the take ends where it starts), so no step jumps.
            if (carriageVelocity.sqrMagnitude > 25f) carriageVelocity = Vector3.zero;
        }

        void Sample(double time)
        {
            if (clip == null || clip.length <= 0f) return;
            float t = (float)(time % clip.length);
            if (t < 0f) t += clip.length;
            clip.SampleAnimation(gameObject, t);
        }
    }
}
